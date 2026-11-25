using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WaterTurn
{
    /// <summary>
    /// "果冻潮池" — the side-scrolling jelly platformer ported from
    /// Docs/waterturn-fish-run.html.  The world and the HUD are uGUI, the fish is
    /// the project's existing pixel art, and the five chapter entrances are stood
    /// on and entered with E (nothing is triggered by brushing past them).
    /// </summary>
    public sealed partial class JellyRunGame : MonoBehaviour
    {
        public const string SceneName = "JellyRun";
        public const string ClearedKey = "WaterTurn.JellyRun.Cleared.v1";
        public const string MutedKey = "WaterTurn.JellyRun.Muted.v1";

        const float Step = 1f / 120f;
        const float R = JellyRunData.FishRadius;
        const float Gravity = 1800f;
        const float SwimAccel = 1500f;
        const float SwimMax = 250f;
        const float SwimDrag = 6.2f;
        const float Buoyancy = .09f;
        const float JumpBase = 560f;
        const float JumpCharge = 430f;
        const float AirAccel = 1000f;
        const float AirMax = 300f;
        const float DoubleJump = 430f;
        const float FlopSpeed = 118f;
        const float FlopHop = 330f;
        const float ChargeGain = 2.4f;
        const float ChargeDecay = .55f;
        const float MoistDrain = 13f;
        const float MoistFill = 240f;
        const float DeathSeconds = 1.02f;
        const int PortalCooldown = 2;

        enum Phase { Intro, Play, Dead, Entering, Win }

        Phase phase = Phase.Intro;
        float time, elapsed, charge, moisture = 100f, deadTimer, shake, flash, portalCool, zoneT;
        float acc;
        int deaths, zoneIdx = -1;
        int clearedMask;
        bool muted, helpOpen, god;
        bool jumpQueued, interactQueued;
        bool entering;
        string toastMsg = "";
        float toastT;
        int nearestEntrance = -1;

        Vector2 fishPos, fishVel;
        bool inJelly, onGround, usedDouble;
        int jellyIndex = -1, ignoreJelly = -1, checkpointJelly = 0;
        Vector2 checkpoint;
        FishFacing facing = FishFacing.Right;
        float animClock;

        Vector2 cam;

        Font font;
        Canvas canvas;
        RectTransform canvasRect, worldRoot, bgRoot, hudRoot;

        readonly List<JellyView> jellies = new List<JellyView>();
        readonly List<SawView> saws = new List<SawView>();
        DoorView door;
        SwitchView switchView;
        readonly List<EntranceView> entrances = new List<EntranceView>();
        PortalView portal;
        FishView fishView;
        readonly List<Particle> particles = new List<Particle>();
        readonly List<Floater> floaters = new List<Floater>();
        JellyRunAudio sfx;

        RawImage caveFar, caveNear, stalFar, stalNear;
        readonly List<Image> shafts = new List<Image>();
        readonly List<Image> motes = new List<Image>();
        Image redFlash;
        Image wellValveGlow, wellValveRing;
        RectTransform wellValveRingRect;
        Text wellValveLabel;
        bool wellHintShown;

        sealed class JellyView
        {
            public int index;
            public JellyRunData.JellyF spec;
            public float x, y, w, h, squash, glow, seed;
            public bool flooded;
            public RectTransform rect, bodyRect;
            public JellyGraphic body;
            public Image glowImage, shadow;
            public readonly List<Ripple> ripples = new List<Ripple>();
            public JellyRunData.RectF Box => new JellyRunData.RectF(x, y, w, h);
        }

        sealed class Ripple
        {
            public Image image;
            public Vector2 position;
            public float t;
        }

        sealed class SawView
        {
            public JellyRunData.SawF spec;
            public RectTransform rect;
            public Image image, glow;
            public float t, x, y, angle;
        }

        sealed class DoorView
        {
            public RectTransform rect, bars;
            public Image frame;
            public float open;
        }

        sealed class SwitchView
        {
            public RectTransform root, crystal;
            public Image glow, crystalImage;
            public bool on;
            public float pulse;
        }

        sealed class EntranceView
        {
            public int index;
            public JellyRunData.Entrance spec;
            public RectTransform root, pearlRect;
            public Image glow, ring, pearl, beam;
            public readonly Image[] pips = new Image[4];
            public Text title, range;
            public float pulse;
        }

        sealed class PortalView
        {
            public RectTransform root;
            public Image glow, core;
            public readonly List<Image> rings = new List<Image>();
            public readonly List<Image> arcs = new List<Image>();
            public Text count, state;
            public bool open;
        }

        sealed class FishView
        {
            public RectTransform root, imageRect, chargeRect, hintRect;
            public Image image, glow, charge, hint;
            public Sprite[] swimR, swimL, swimU, swimD, jumpR, jumpL, jumpU, jumpD, death;
        }

        sealed class Particle
        {
            public RectTransform rect;
            public Image image;
            public Vector2 pos, vel;
            public float t, life, size;
            public Color color;
            public bool rise, active;
        }

        sealed class Floater
        {
            public RectTransform rect;
            public Text text;
            public float t, life;
            public bool active;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != SceneName) return;
            if (FindFirstObjectByType<JellyRunGame>() == null)
                new GameObject("JellyRun").AddComponent<JellyRunGame>();
        }

        void Awake()
        {
            muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
            clearedMask = GameSession.JellyRunCleared;
            god = false;
            BuildAll();
            ResetForSceneLoad();
        }

        void OnDestroy()
        {
            PlayerPrefs.Save();
        }

        void Update()
        {
            ReadDiscreteInput();

            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            acc += dt;
            int guard = 0;
            while (acc >= Step && guard++ < 8)
            {
                Tick(Step);
                acc -= Step;
            }
            float renderDt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            UpdatePresentation(renderDt);
            RefreshHud(renderDt);
        }

        void ReadDiscreteInput()
        {
            var k = Keyboard.current;
            if (k == null) return;

            if (k.mKey.wasPressedThisFrame)
            {
                sfx.SetMuted(!sfx.Muted);
                ShowToast(sfx.Muted ? "已静音" : "声音已打开");
            }
            if (k.hKey.wasPressedThisFrame && phase != Phase.Win)
                ToggleHelp();
            if (k.escapeKey.wasPressedThisFrame)
            {
                if (helpOpen) ToggleHelp();
                else ReturnToHub();
                return;
            }
            if (k.rKey.wasPressedThisFrame && phase == Phase.Play)
            {
                ReturnToCheckpoint();
                return;
            }
            if (phase == Phase.Intro)
            {
                if (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame)
                    StartRun();
                return;
            }
            if (phase == Phase.Win)
            {
                if (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)
                    RestartRun();
                return;
            }
            if (phase != Phase.Play || helpOpen) return;
            if (k.spaceKey.wasPressedThisFrame) jumpQueued = true;
            if (k.eKey.wasPressedThisFrame) interactQueued = true;
        }

        void Tick(float dt)
        {
            time += dt;
            if (shake > 0f) shake = Mathf.Max(0f, shake - dt * 46f);
            if (flash > 0f) flash = Mathf.Max(0f, flash - dt * 2.2f);
            if (toastT > 0f) toastT -= dt;
            if (portalCool > 0f) portalCool -= dt;
            if (zoneT > 0f) zoneT -= dt;
            UpdateWorld(dt);

            if (phase == Phase.Dead)
            {
                deadTimer += dt;
                if (deadTimer >= DeathSeconds) RespawnFish();
                return;
            }
            if (phase != Phase.Play) { jumpQueued = false; interactQueued = false; return; }
            if (helpOpen) { jumpQueued = false; interactQueued = false; return; }

            elapsed += dt;
            StepFish(dt);
            if (phase != Phase.Play) { jumpQueued = false; interactQueued = false; return; }

            nearestEntrance = FindNearEntrance();
            if (interactQueued)
            {
                interactQueued = false;
                if (nearestEntrance >= 0) EnterChapter(nearestEntrance);
                else ShowToast("走到潮心石台上再按 E");
            }
            CheckPortal();
            UpdateCamera(dt);
            UpdateZone();
        }

        void StepFish(float dt)
        {
            var k = Keyboard.current;
            int ix = 0, iy = 0;
            if (k != null)
            {
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) ix += 1;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) ix -= 1;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) iy += 1;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) iy -= 1;
            }

            if (inJelly) UpdateSwim(dt, ix, iy);
            else UpdateAir(dt, ix);
        }

        void UpdateSwim(float dt, int ix, int iy)
        {
            var j = jellies[jellyIndex];
            fishVel.x += ix * SwimAccel * dt;
            fishVel.y += iy * SwimAccel * .85f * dt;
            fishVel.y += Gravity * Buoyancy * dt;

            float damp = Mathf.Exp(-SwimDrag * dt);
            fishVel *= damp;
            float speed = fishVel.magnitude;
            if (speed > SwimMax) fishVel *= SwimMax / speed;

            fishPos += fishVel * dt;
            fishPos.x = Mathf.Clamp(fishPos.x, j.x + R + 2f, j.x + j.w - R - 2f);
            fishPos.y = Mathf.Clamp(fishPos.y, j.y + R + 2f, j.y + j.h - R - 2f);

            if (fishVel.y > 30f)
            {
                charge += (fishVel.y / SwimMax) * ChargeGain * dt;
                if (Random.value < dt * 10f) Bubble(j, fishPos.x + Random.Range(-14f, 14f), fishPos.y + 8f);
            }
            charge = Mathf.Clamp(charge - ChargeDecay * dt, 0f, 1f);

            moisture = Mathf.Min(100f, moisture + MoistFill * dt);
            onGround = false;
            if (Mathf.Abs(fishVel.y) > Mathf.Abs(fishVel.x) && Mathf.Abs(fishVel.y) > 25f)
                facing = fishVel.y > 0f ? FishFacing.Down : FishFacing.Up;
            else if (Mathf.Abs(fishVel.x) > 20f) facing = fishVel.x > 0f ? FishFacing.Right : FishFacing.Left;
            else if (ix != 0) facing = ix > 0 ? FishFacing.Right : FishFacing.Left;

            if (j.spec.valve != Vector2.zero && !j.flooded)
            {
                var valve = j.spec.valve;
                if (Mathf.Abs(fishPos.x - valve.x) < 70f && Mathf.Abs(fishPos.y - valve.y) < 60f)
                    StartFlood(j);
            }

            if (jumpQueued)
            {
                jumpQueued = false;
                float power = JumpBase + JumpCharge * charge;
                fishVel.y = -power;
                fishVel.x = fishVel.x * 1.3f + (Mathf.Abs(fishVel.x) > 40f ? Mathf.Sign(fishVel.x) * 45f : 0f);
                inJelly = false;
                ignoreJelly = jellyIndex;
                jellyIndex = -1;
                usedDouble = false;
                j.squash = 1.25f;
                AddRipple(j, fishPos.x);
                Splash(fishPos.x, j.y, Mathf.Clamp(power / 1200f, .3f, 1f));
                shake = Mathf.Max(shake, 3f + charge * 3f);
                sfx.Play("jump", .8f + charge * .5f, 1f);
                charge = 0f;
            }
        }

        void UpdateAir(float dt, int ix)
        {
            if (jumpQueued)
            {
                jumpQueued = false;
                if (onGround)
                {
                    if (moisture > 8f)
                    {
                        fishVel.y = -FlopHop;
                        onGround = false;
                        moisture -= 8f;
                        Burst(fishPos.x, fishPos.y + 10f, 5, new Color(190 / 255f, 230 / 255f, 1f, 1f));
                        sfx.Play("hop");
                    }
                    else ShowToast("太干了，蹦不动了");
                }
                else if (!usedDouble)
                {
                    usedDouble = true;
                    fishVel.y = -DoubleJump;
                    fishVel.x += (fishVel.x >= 0f ? 1f : -1f) * 60f;
                    Burst(fishPos.x, fishPos.y, 9, new Color(150 / 255f, 1f, 235 / 255f, 1f));
                    sfx.Play("dbl");
                }
            }

            if (onGround)
            {
                fishVel.x += ix * 420f * dt;
                fishVel.x = Mathf.Clamp(fishVel.x, -FlopSpeed, FlopSpeed);
                if (ix == 0) fishVel.x *= Mathf.Exp(-9f * dt);
            }
            else
            {
                fishVel.x += ix * AirAccel * dt;
                fishVel.x = Mathf.Clamp(fishVel.x, -AirMax, AirMax);
            }
            fishVel.y += Gravity * dt;

            if (Mathf.Abs(fishVel.y) > Mathf.Abs(fishVel.x) && Mathf.Abs(fishVel.y) > 40f)
                facing = fishVel.y > 0f ? FishFacing.Down : FishFacing.Up;
            else if (Mathf.Abs(fishVel.x) > 20f) facing = fishVel.x > 0 ? FishFacing.Right : FishFacing.Left;

            MoveAndCollide(dt);

            if (ignoreJelly >= 0 && !Overlap(FishBox(), jellies[ignoreJelly].Box)) ignoreJelly = -1;
            for (int i = 0; i < jellies.Count; i++)
            {
                if (i == ignoreJelly) continue;
                if (!Overlap(FishBox(), jellies[i].Box)) continue;
                EnterJelly(i);
                return;
            }

            for (int i = 0; i < JellyRunData.Spikes.Length; i++)
            {
                var s = JellyRunData.Spikes[i];
                var box = new JellyRunData.RectF(s.x + 3f, s.y + (s.up ? 8f : 3f), s.w - 6f, s.h - 8f);
                if (Overlap(FishBox(), box)) { Die(s.up ? "踩到尖刺了" : "撞上天花板的尖刺了"); return; }
            }
            for (int i = 0; i < saws.Count; i++)
            {
                var s = saws[i];
                if (Vector2.Distance(fishPos, new Vector2(s.x, s.y)) < s.spec.r + 6f) { Die("被锯片扫到了"); return; }
            }
            if (fishPos.y > JellyRunData.WorldH + 90f) { Die("掉下深渊了"); return; }

            if (!OnEntranceLedge())
            {
                moisture -= MoistDrain * dt;
                if (moisture <= 0f)
                {
                    moisture = 0f;
                    Die("水分耗尽了");
                    return;
                }
            }
            moisture = Mathf.Clamp(moisture, 0f, 100f);
        }

        bool OnEntranceLedge()
        {
            if (!onGround) return false;
            for (int i = 0; i < JellyRunData.Entrances.Length; i++)
            {
                var e = JellyRunData.Entrances[i];
                if (fishPos.x >= e.platformX - 4f && fishPos.x <= e.platformX + e.platformW + 4f &&
                    Mathf.Abs(fishPos.y - (e.top - R)) < 26f)
                    return true;
            }
            return false;
        }

        void MoveAndCollide(float dt)
        {
            fishPos.x += fishVel.x * dt;
            var box = FishBox();
            for (int i = 0; i < JellyRunData.Floors.Length; i++)
            {
                var s = JellyRunData.Floors[i];
                if (!Overlap(box, s)) continue;
                if (fishVel.x > 0f) fishPos.x = s.x - R - .01f;
                else if (fishVel.x < 0f) fishPos.x = s.Right + R + .01f;
                fishVel.x = 0f;
                box = FishBox();
            }
            for (int i = 0; i < JellyRunData.EntrancePlatforms.Length; i++)
            {
                var s = JellyRunData.EntrancePlatforms[i];
                if (!Overlap(box, s)) continue;
                if (fishVel.x > 0f) fishPos.x = s.x - R - .01f;
                else if (fishVel.x < 0f) fishPos.x = s.Right + R + .01f;
                fishVel.x = 0f;
                box = FishBox();
            }
            if (door == null || door.open < .85f)
            {
                var s = JellyRunData.Door;
                if (Overlap(box, s))
                {
                    if (fishVel.x > 0f) fishPos.x = s.x - R - .01f;
                    else if (fishVel.x < 0f) fishPos.x = s.Right + R + .01f;
                    fishVel.x = 0f;
                    box = FishBox();
                }
            }

            fishPos.y += fishVel.y * dt;
            box = FishBox();
            bool wasDown = fishVel.y > 0f;
            float impactSpeed = fishVel.y;
            onGround = false;
            for (int i = 0; i < JellyRunData.Floors.Length; i++)
            {
                var s = JellyRunData.Floors[i];
                if (!Overlap(box, s)) continue;
                if (fishVel.y > 0f) { fishPos.y = s.y - R - .01f; onGround = true; }
                else if (fishVel.y < 0f) fishPos.y = s.Bottom + R + .01f;
                fishVel.y = 0f;
                box = FishBox();
            }
            for (int i = 0; i < JellyRunData.EntrancePlatforms.Length; i++)
            {
                var s = JellyRunData.EntrancePlatforms[i];
                if (!Overlap(box, s)) continue;
                if (fishVel.y > 0f) { fishPos.y = s.y - R - .01f; onGround = true; }
                else if (fishVel.y < 0f) fishPos.y = s.Bottom + R + .01f;
                fishVel.y = 0f;
                box = FishBox();
            }
            if (door == null || door.open < .85f)
            {
                var s = JellyRunData.Door;
                if (Overlap(box, s))
                {
                    if (fishVel.y > 0f) { fishPos.y = s.y - R - .01f; onGround = true; }
                    else if (fishVel.y < 0f) fishPos.y = s.Bottom + R + .01f;
                    fishVel.y = 0f;
                }
            }

            if (fishPos.y < R) { fishPos.y = R; if (fishVel.y < 0f) fishVel.y = 0f; }
            if (fishPos.x < R) fishPos.x = R;
            if (fishPos.x > JellyRunData.WorldW - R) fishPos.x = JellyRunData.WorldW - R;
            if (wasDown && onGround && impactSpeed > 140f) sfx.Play("land", 1f, .35f);
        }

        void EnterJelly(int index)
        {
            var j = jellies[index];
            float impact = Mathf.Abs(fishVel.y);
            inJelly = true;
            jellyIndex = index;
            onGround = false;
            fishVel *= .6f;
            fishVel.y *= .7f;
            fishPos.x = Mathf.Clamp(fishPos.x, j.x + R + 2f, j.x + j.w - R - 2f);
            fishPos.y = Mathf.Clamp(fishPos.y, j.y + R + 2f, j.y + j.h - R - 2f);
            j.squash = Mathf.Clamp(.5f + impact / 700f, 0f, 1.3f);
            j.glow = 1f;
            AddRipple(j, fishPos.x);
            if (impact > 160f)
            {
                Burst(fishPos.x, j.y, 8, new Color(130 / 255f, 1f, 225 / 255f, 1f));
                sfx.Play("splash");
            }
            checkpointJelly = index;
            checkpoint = new Vector2(j.x + j.w * .5f, j.y + Mathf.Min(j.h * .5f, 55f));
            moisture = Mathf.Min(100f, moisture + 45f);
            if (index == 9 && !j.flooded && !wellHintShown)
            {
                wellHintShown = true;
                ShowToast("井底有个阀门 · 潜到最深处碰它，潮水会把你托上去");
            }
        }

        void StartFlood(JellyView j)
        {
            if (j.flooded) return;
            j.flooded = true;
            sfx.Play("flood");
            shake = Mathf.Max(shake, 9f);
            ShowToast("阀门被压开，潮水正在涌上来");
            for (int i = 0; i < 22; i++)
                AddParticle(new Vector2(j.x + Random.Range(6f, j.w - 6f), j.y + j.h),
                    new Vector2(Random.Range(-40f, 40f), Random.Range(-320f, -120f)), .9f,
                    new Color(140 / 255f, 1f, 235 / 255f, 1f), Random.Range(1.5f, 3.4f), false);
        }

        void Die(string reason)
        {
            if (phase != Phase.Play || god) return;
            phase = Phase.Dead;
            deadTimer = 0f;
            deaths++;
            shake = 14f;
            flash = 1f;
            Burst(fishPos.x, fishPos.y, 20, new Color(1f, 130 / 255f, 130 / 255f, 1f));
            sfx.Play("die");
            ShowToast(reason);
        }

        void RespawnFish()
        {
            int index = Mathf.Clamp(checkpointJelly, 0, jellies.Count - 1);
            var j = jellies[index];
            inJelly = true;
            jellyIndex = index;
            ignoreJelly = -1;
            fishPos = checkpoint;
            fishPos.x = Mathf.Clamp(fishPos.x, j.x + R + 2f, j.x + j.w - R - 2f);
            fishPos.y = Mathf.Clamp(fishPos.y, j.y + R + 2f, j.y + j.h - R - 2f);
            fishVel = Vector2.zero;
            onGround = false;
            usedDouble = false;
            moisture = 100f;
            charge = 0f;
            phase = Phase.Play;
            deadTimer = 0f;
            jumpQueued = false;
            interactQueued = false;
            SnapCamera();
            j.glow = 1f;
            AddRipple(j, fishPos.x);
        }

        void ReturnToCheckpoint()
        {
            if (phase != Phase.Play) return;
            int index = Mathf.Clamp(checkpointJelly, 0, jellies.Count - 1);
            var j = jellies[index];
            inJelly = true;
            jellyIndex = index;
            ignoreJelly = -1;
            fishPos = checkpoint;
            fishPos.x = Mathf.Clamp(fishPos.x, j.x + R + 2f, j.x + j.w - R - 2f);
            fishPos.y = Mathf.Clamp(fishPos.y, j.y + R + 2f, j.y + j.h - R - 2f);
            fishVel = Vector2.zero;
            onGround = false;
            usedDouble = false;
            moisture = 100f;
            charge = 0f;
            ShowToast("回到最近的水池");
            sfx.Play("splash");
            j.glow = 1f;
        }

        int FindNearEntrance()
        {
            if (!onGround || inJelly) return -1;
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < JellyRunData.Entrances.Length; i++)
            {
                var e = JellyRunData.Entrances[i];
                if (fishPos.x < e.platformX - 10f || fishPos.x > e.platformX + e.platformW + 10f) continue;
                if (Mathf.Abs(fishPos.y - (e.top - R)) > 40f) continue;
                float d = Mathf.Abs(fishPos.x - e.entryX);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        void EnterChapter(int chapter)
        {
            if (entering || phase != Phase.Play) return;
            if (WaterFx.Busy) { ShowToast("正在转场"); return; }
            entering = true;
            phase = Phase.Entering;
            sfx.Play("enter");
            int start = GameSession.BeginJellyRunChapter(chapter);
            var e = JellyRunData.Entrances[Mathf.Clamp(chapter, 0, 4)];
            ShowToast("潮心共鸣 · " + e.Title + " · 从第 " + (start + 1).ToString("00") + " 关开始");
            WaterFx.GoToLevel(start);
        }

        void CheckPortal()
        {
            if (!Overlap(FishBox(), JellyRunData.Portal)) return;
            if (clearedMask == 0xFFFFF)
            {
                WinRun();
                return;
            }
            if (portalCool > 0f) return;
            portalCool = PortalCooldown;
            int left = 20 - CountCleared(clearedMask);
            ShowToast("终点还封印着 · 还差 " + left + " 关");
            sfx.Play("nope");
            shake = Mathf.Max(shake, 5f);
            Burst(fishPos.x, fishPos.y - 16f, 10, new Color(1f, 150 / 255f, 150 / 255f, 1f));
        }

        void WinRun()
        {
            if (phase == Phase.Win) return;
            phase = Phase.Win;
            flash = .6f;
            sfx.Play("win");
            Burst(JellyRunData.Portal.x + JellyRunData.Portal.w * .5f,
                JellyRunData.Portal.y + JellyRunData.Portal.h * .5f, 40,
                new Color(1f, 220 / 255f, 140 / 255f, 1f));
            ShowWinOverlay();
        }

        Vector2 CameraTarget()
        {
            float w = canvasRect != null && canvasRect.rect.width > 100f ? canvasRect.rect.width : JellyRunData.ViewW;
            float h = canvasRect != null && canvasRect.rect.height > 100f ? canvasRect.rect.height : JellyRunData.ViewH;
            float tx = fishPos.x - w * .42f + Mathf.Clamp(fishVel.x * .35f, -130f, 170f);
            float ty = fishPos.y - h * .58f;
            return new Vector2(
                Mathf.Clamp(tx, 0f, Mathf.Max(0f, JellyRunData.WorldW - w)),
                Mathf.Clamp(ty, 0f, Mathf.Max(0f, JellyRunData.WorldH - h)));
        }

        void UpdateCamera(float dt)
        {
            Vector2 target = CameraTarget();
            float k = 1f - Mathf.Exp(-6.5f * dt);
            cam.x += (target.x - cam.x) * k;
            cam.y += (target.y - cam.y) * k;
        }

        void SnapCamera()
        {
            cam = CameraTarget();
        }

        void UpdateZone()
        {
            int zi = 0;
            for (int i = 0; i < JellyRunData.Zones.Length; i++)
                if (fishPos.x >= JellyRunData.Zones[i].x) zi = i;
            if (zi != zoneIdx)
            {
                zoneIdx = zi;
                zoneT = 3f;
            }
        }

        void UpdateWorld(float dt)
        {
            for (int i = 0; i < jellies.Count; i++)
            {
                var j = jellies[i];
                if (j.spec.floodTo >= 0f && j.flooded && j.y > j.spec.floodTo)
                {
                    float ny = Mathf.Max(j.spec.floodTo, j.y - 150f * dt);
                    float d = j.y - ny;
                    j.y = ny;
                    j.h += d;
                    if (Random.value < dt * 14f) AddParticle(new Vector2(j.x + Random.Range(6f, j.w - 6f), j.y + 4f),
                        new Vector2(Random.Range(-12f, 12f), Random.Range(-46f, -22f)), 1f,
                        new Color(190 / 255f, 1f, 245 / 255f, 1f), Random.Range(1f, 2.2f), true);
                }
                if (j.squash > 0f) j.squash = Mathf.Max(0f, j.squash - dt * 2.8f);
                if (j.glow > 0f) j.glow = Mathf.Max(0f, j.glow - dt * 1.8f);
                for (int r = j.ripples.Count - 1; r >= 0; r--)
                {
                    j.ripples[r].t += dt;
                    if (j.ripples[r].t > 1.1f) { RecycleRipple(j.ripples[r]); j.ripples.RemoveAt(r); }
                }
            }

            if (door.open < 1f)
            {
                bool shouldOpen = switchView != null && switchView.on;
                if (shouldOpen) door.open = Mathf.Min(1f, door.open + dt * 2.4f);
            }

            if (switchView != null && switchView.pulse > 0f) switchView.pulse = Mathf.Max(0f, switchView.pulse - dt * 1.6f);

            for (int i = 0; i < saws.Count; i++)
            {
                var s = saws[i];
                s.t += dt * s.spec.speed;
                float k = .5f - .5f * Mathf.Cos(s.t);
                s.x = s.spec.ax + (s.spec.bx - s.spec.ax) * k;
                s.y = s.spec.ay + (s.spec.by - s.spec.ay) * k;
                s.angle += dt * 7.5f;
            }

            if (switchView != null && !switchView.on &&
                Vector2.Distance(fishPos, JellyRunData.Switch.position) < JellyRunData.Switch.radius + 16f)
            {
                switchView.on = true;
                switchView.pulse = 1f;
                Burst(JellyRunData.Switch.position.x, JellyRunData.Switch.position.y, 22,
                    new Color(1f, 215 / 255f, 130 / 255f, 1f));
                shake = Mathf.Max(shake, 6f);
                sfx.Play("switch");
                ShowToast("机关启动 · 闸门打开了");
            }

            nearestEntrance = FindNearEntrance();
        }

        JellyRunData.RectF FishBox() => new JellyRunData.RectF(fishPos.x - R, fishPos.y - R, R * 2f, R * 2f);
        static bool Overlap(JellyRunData.RectF a, JellyRunData.RectF b) => a.Overlaps(b);

        static int CountCleared(int mask)
        {
            int n = 0;
            for (int i = 0; i < 20; i++) if ((mask & (1 << i)) != 0) n++;
            return n;
        }

        static bool LevelCleared(int level) => (GameSession.JellyRunCleared & (1 << level)) != 0;

        bool ChapterComplete(int chapter)
        {
            int mask = GameSession.JellyRunCleared;
            for (int i = chapter * 4; i < chapter * 4 + 4; i++)
                if ((mask & (1 << i)) == 0) return false;
            return true;
        }

        int NearestJelly(Vector2 p)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < jellies.Count; i++)
            {
                var j = jellies[i];
                float d = Vector2.Distance(p, new Vector2(j.x + j.w * .5f, j.y + j.h * .5f));
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        void ResetForSceneLoad()
        {
            int returnEntrance = GameSession.ReturnEntrance;
            GameSession.ReturnEntrance = -1;
            GameSession.FromJellyRun = false;
            GameSession.InJellyRunChain = false;
            clearedMask = GameSession.JellyRunCleared;

            if (returnEntrance >= 0 && returnEntrance < JellyRunData.Entrances.Length)
            {
                SpawnAtEntrance(returnEntrance);
                phase = Phase.Play;
                if (introRoot != null) introRoot.gameObject.SetActive(false);
                var e = JellyRunData.Entrances[returnEntrance];
                int done = 0;
                for (int i = e.FirstLevel; i <= e.LastLevel; i++) if (LevelCleared(i)) done++;
                ShowToast("回到 " + e.Title + " · " + done + " / 4");
                if (GameSession.LastCompletedLevel >= 0)
                    ShowToast("第 " + (GameSession.LastCompletedLevel + 1).ToString("00") + " 关通过 · " +
                              JellyRunData.LevelName(GameSession.LastCompletedLevel));
                GameSession.LastCompletedLevel = -1;
                RefreshHud(0f);
                SnapCamera();
                return;
            }

            ResetFishToStart();
            phase = Phase.Intro;
            if (introRoot != null) introRoot.gameObject.SetActive(true);
            if (winRoot != null) winRoot.gameObject.SetActive(false);
            SnapCamera();
            RefreshHud(0f);
        }

        void ResetFishToStart()
        {
            var j = jellies[0];
            inJelly = true;
            jellyIndex = 0;
            ignoreJelly = -1;
            fishPos = new Vector2(j.x + 52f, j.y + 62f);
            fishVel = Vector2.zero;
            onGround = false;
            usedDouble = false;
            moisture = 100f;
            charge = 0f;
            deaths = 0;
            elapsed = 0f;
            checkpointJelly = 0;
            checkpoint = new Vector2(fishPos.x, fishPos.y);
            facing = FishFacing.Right;
            nearestEntrance = -1;
            entering = false;
            jumpQueued = false;
            interactQueued = false;
            wellHintShown = false;
        }

        void SpawnAtEntrance(int entrance)
        {
            var e = JellyRunData.Entrances[entrance];
            inJelly = false;
            jellyIndex = -1;
            ignoreJelly = -1;
            fishPos = new Vector2(e.entryX, e.top - R);
            fishVel = Vector2.zero;
            onGround = true;
            usedDouble = false;
            moisture = 100f;
            charge = 0f;
            facing = FishFacing.Right;
            checkpointJelly = NearestJelly(fishPos);
            var j = jellies[checkpointJelly];
            checkpoint = new Vector2(j.x + j.w * .5f, j.y + Mathf.Min(j.h * .5f, 55f));
        }

        void StartRun()
        {
            ResetFishToStart();
            phase = Phase.Play;
            if (introRoot != null) introRoot.gameObject.SetActive(false);
            ShowToast("按 S 下潜蓄力，空格弹出；找到五处潮心，站在石台上按 E");
            SnapCamera();
        }

        void RestartRun()
        {
            if (winRoot != null) winRoot.gameObject.SetActive(false);
            ResetFishToStart();
            phase = Phase.Play;
            SnapCamera();
        }

        void ReturnToHub()
        {
            if (phase == Phase.Entering) return;
            sfx.Play("enter");
            WaterFx.GoToHub(-1);
        }

        public string DebugState()
        {
            return "phase=" + phase + " pos=" + fishPos.x.ToString("0") + "," + fishPos.y.ToString("0") +
                   " mode=" + (inJelly ? "swim" : "air") + " ground=" + onGround +
                   " moisture=" + moisture.ToString("0") + " cleared=" + CountCleared(GameSession.JellyRunCleared) +
                   " deaths=" + deaths + " cam=" + cam.x.ToString("0") + "," + cam.y.ToString("0") +
                   " nearEntrance=" + nearestEntrance + " vel=" + fishVel.x.ToString("0") + "," + fishVel.y.ToString("0");
        }

        public void DebugGoto(float x, float y)
        {
            fishPos = new Vector2(x, y);
            fishVel = Vector2.zero;
            inJelly = false;
            jellyIndex = -1;
            ignoreJelly = -1;
            onGround = false;
            usedDouble = false;
            phase = Phase.Play;
            if (introRoot != null) introRoot.gameObject.SetActive(false);
            SnapCamera();
        }

        public void DebugEnterChapter(int chapter) { phase = Phase.Play; nearestEntrance = -1; EnterChapter(chapter); }
        public void DebugInteract() { interactQueued = true; }
        public void DebugJump() { jumpQueued = true; }
        public void DebugSetCharge(float value) { charge = Mathf.Clamp01(value); }
        public void DebugPlaceOnEntrance(int index)
        {
            if (index < 0 || index >= JellyRunData.Entrances.Length) return;
            var e = JellyRunData.Entrances[index];
            DebugGoto(e.entryX, e.top - R);
        }
        public void DebugSetGod(bool value) { god = value; }
        public void DebugDie() { phase = Phase.Play; Die("调试死亡"); }
        public void DebugWin()
        {
            clearedMask = 0xFFFFF;
            PlayerPrefs.SetInt(ClearedKey, clearedMask);
            PlayerPrefs.Save();
            WinRun();
        }
        public void DebugClearLevel(int level)
        {
            if (level < 0 || level >= 20) return;
            clearedMask |= 1 << level;
            PlayerPrefs.SetInt(ClearedKey, clearedMask & 0xFFFFF);
            PlayerPrefs.Save();
        }
        public void DebugClearAll()
        {
            clearedMask = 0xFFFFF;
            PlayerPrefs.SetInt(ClearedKey, clearedMask);
            PlayerPrefs.Save();
        }
        public void DebugResetProgress()
        {
            clearedMask = 0;
            PlayerPrefs.DeleteKey(ClearedKey);
            PlayerPrefs.Save();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(.3f, .9f, .8f, .15f);
            Gizmos.DrawWireCube(new Vector3(JellyRunData.WorldW * .5f, -JellyRunData.WorldH * .5f, 0f),
                new Vector3(JellyRunData.WorldW, JellyRunData.WorldH, 1f));
            for (int i = 0; i < JellyRunData.Entrances.Length; i++)
            {
                var e = JellyRunData.Entrances[i];
                Gizmos.color = new Color(1f, .82f, .4f, .95f);
                Gizmos.DrawWireCube(new Vector3(e.entryX, -(e.top - R), 0f), new Vector3(34f, 34f, 1f));
                Gizmos.color = new Color(.45f, .95f, .8f, .35f);
                var p = e.Platform;
                Gizmos.DrawWireCube(new Vector3(p.x + p.w * .5f, -(p.y + p.h * .5f), 0f), new Vector3(p.w, p.h, 1f));
            }
            Gizmos.color = new Color(1f, .9f, .5f, .7f);
            var portal = JellyRunData.Portal;
            Gizmos.DrawWireCube(new Vector3(portal.x + portal.w * .5f, -(portal.y + portal.h * .5f), 0f),
                new Vector3(portal.w, portal.h, 1f));
        }
    }
}
