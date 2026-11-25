using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace WaterTurn
{
    /// <summary>
    /// Scene graph construction and per-frame presentation for the jelly run.
    /// The world is one huge RectTransform moved to emulate the HTML camera;
    /// background layers and HUD are screen-fixed.
    /// </summary>
    public sealed partial class JellyRunGame
    {
        void BuildAll()
        {
            font = LoadFont();
            BuildCanvas();
            BuildBackground();
            BuildWorld();
            sfx = gameObject.AddComponent<JellyRunAudio>();
            sfx.Init(muted);
            BuildHud();
        }

        static Font LoadFont()
        {
            var f = Font.CreateDynamicFontFromOSFont(
                new[] {"Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial"}, 28);
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f;
        }

        void BuildCanvas()
        {
            var canvasGo = new GameObject("JellyRunCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(JellyRunData.ViewW, JellyRunData.ViewH);
            scaler.matchWidthOrHeight = .5f;
            canvasRect = (RectTransform)canvasGo.transform;

            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            bgRoot = Stretch("Background");
            worldRoot = new GameObject("World", typeof(RectTransform)).GetComponent<RectTransform>();
            worldRoot.SetParent(canvasRect, false);
            worldRoot.anchorMin = worldRoot.anchorMax = new Vector2(0f, 1f);
            worldRoot.pivot = new Vector2(0f, 1f);
            worldRoot.sizeDelta = new Vector2(JellyRunData.WorldW, JellyRunData.WorldH);
            worldRoot.anchoredPosition = Vector2.zero;

            hudRoot = Stretch("Hud");
        }

        RectTransform Stretch(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvasRect, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        // ------------------------------------------------------------- background

        void BuildBackground()
        {
            var grad = ScreenImage("NightGradient", JellyRunArt.Background, Color.white);
            grad.type = Image.Type.Simple;

            caveFar = ScreenRaw("CaveFar", JellyRunArt.Cave(0).texture, new Color(1f, 1f, 1f, .60f));
            caveNear = ScreenRaw("CaveNear", JellyRunArt.Cave(1).texture, new Color(1f, 1f, 1f, .82f));
            stalFar = ScreenRaw("StalFar", JellyRunArt.Stalactites(0).texture, new Color(1f, 1f, 1f, .75f));
            stalNear = ScreenRaw("StalNear", JellyRunArt.Stalactites(1).texture, new Color(1f, 1f, 1f, .95f));
            foreach (var stal in new[] {stalFar, stalNear})
            {
                var rt = stal.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(.5f, 1f);
                rt.sizeDelta = new Vector2(0f, 300f);
                rt.anchoredPosition = Vector2.zero;
            }

            for (int i = 0; i < 5; i++)
            {
                var shaft = ScreenImage("Shaft " + i, JellyRunArt.Shaft, new Color(1f, 1f, 1f, .10f));
                var rt = shaft.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
                rt.pivot = new Vector2(.5f, .5f);
                rt.sizeDelta = new Vector2(90f + (i % 3) * 30f, 760f);
                rt.anchoredPosition = new Vector2(-380f + i * 190f, 80f);
                rt.localRotation = Quaternion.Euler(0f, 0f, -12f + (i % 4) * 3f);
                shaft.raycastTarget = false;
                shafts.Add(shaft);
            }

            for (int i = 0; i < 46; i++)
            {
                var mote = ScreenImage("Mote " + i, JellyRunArt.Dot, new Color(.72f, 1f, .95f, .12f + (i % 5) * .035f));
                var rt = mote.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
                rt.pivot = new Vector2(.5f, .5f);
                rt.sizeDelta = Vector2.one * (2f + (i % 3) * 1.2f);
                motes.Add(mote);
            }

            var vignette = ScreenImage("Vignette", JellyRunArt.Vignette, Color.white);
            vignette.type = Image.Type.Simple;
            vignette.raycastTarget = false;
        }

        Image ScreenImage(string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(bgRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        RawImage ScreenRaw(string name, Texture texture, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(bgRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var raw = go.GetComponent<RawImage>();
            raw.texture = texture;
            raw.color = color;
            raw.raycastTarget = false;
            return raw;
        }

        // ------------------------------------------------------------------ world

        void BuildWorld()
        {
            var voidGlow = WorldImage("VoidGlow", JellyRunArt.BottomGlow, Color.white,
                JellyRunData.WorldW * .5f, 860f, JellyRunData.WorldW + 120f, 96f);
            voidGlow.type = Image.Type.Simple;

            for (int i = 0; i < JellyRunData.Floors.Length; i++)
            {
                var f = JellyRunData.Floors[i];
                WorldImage("Floor " + i, JellyRunArt.Floor(i), Color.white,
                    f.x + f.w * .5f, f.y + f.h * .5f, f.w, f.h);
            }

            for (int i = 0; i < JellyRunData.EntrancePlatforms.Length; i++)
            {
                var p = JellyRunData.EntrancePlatforms[i];
                WorldImage("Entrance Ledge " + i, JellyRunArt.Platform(i), Color.white,
                    p.x + p.w * .5f, p.y + p.h * .5f, p.w, p.h);
            }

            // The first shrine floats over the gap between the start pool and the
            // bridge pool; a hanging rock keeps it readable as a carved ledge.
            var e0 = JellyRunData.Entrances[0];
            var support = WorldImage("Entrance Support 0", JellyRunArt.Rounded,
                new Color32(40, 70, 80, 235), e0.entryX, e0.top + 16f + 34f, 18f, 68f);
            support.type = Image.Type.Sliced;

            for (int i = 0; i < JellyRunData.Spikes.Length; i++)
            {
                var s = JellyRunData.Spikes[i];
                WorldImage("Spike " + i, JellyRunArt.Spike(i), Color.white,
                    s.x + s.w * .5f, s.y + s.h * .5f, s.w, s.h);
            }

            BuildDoor();
            BuildJellies();
            BuildWellValve();
            BuildSwitch();
            BuildEntrances();
            BuildPortal();
            BuildSaws();
            BuildFish();
            BuildPools();
        }

        void BuildDoor()
        {
            var d = JellyRunData.Door;
            var root = WorldNode("Door", d.x + d.w * .5f, d.y + d.h * .5f, d.w, d.h);
            root.gameObject.AddComponent<RectMask2D>();
            var frame = root.gameObject.AddComponent<Image>();
            frame.sprite = JellyRunArt.Rounded;
            frame.type = Image.Type.Sliced;
            frame.color = new Color32(19, 40, 51, 255);
            frame.raycastTarget = false;

            door = new DoorView { rect = root, frame = frame };
            var barsGo = new GameObject("Bars", typeof(RectTransform));
            barsGo.transform.SetParent(root, false);
            door.bars = (RectTransform)barsGo.transform;
            door.bars.anchorMin = door.bars.anchorMax = new Vector2(.5f, .5f);
            door.bars.pivot = new Vector2(.5f, .5f);
            door.bars.sizeDelta = new Vector2(d.w - 10f, d.h + 34f);
            door.bars.anchoredPosition = Vector2.zero;

            for (int i = 0; i < 4; i++)
            {
                var bar = ChildImage(door.bars, "Bar " + i, JellyRunArt.Rounded, new Color32(45, 74, 87, 255),
                    new Vector2(-d.w * .5f + 8f + i * (d.w - 16f) / 3.2f, 0f), new Vector2(8f, d.h + 20f));
                bar.type = Image.Type.Sliced;
                ChildImage(bar.rectTransform, "Shine", JellyRunArt.Rounded, new Color32(160, 240, 230, 46),
                    new Vector2(0f, 0f), new Vector2(2f, d.h + 20f));
            }
            for (int i = 0; i < 3; i++)
            {
                var cross = ChildImage(door.bars, "Cross " + i, JellyRunArt.Rounded, new Color32(40, 68, 79, 255),
                    new Vector2(0f, 40f + i * (d.h / 3.2f) - d.h * .5f), new Vector2(d.w - 8f, 7f));
                cross.type = Image.Type.Sliced;
            }
        }

        void BuildJellies()
        {
            for (int i = 0; i < JellyRunData.Jellies.Length; i++)
            {
                var spec = JellyRunData.Jellies[i];
                var view = new JellyView
                {
                    index = i, spec = spec, x = spec.x, y = spec.y, w = spec.w, h = spec.h,
                    seed = i * 1.7f,
                };
                var root = WorldNode("Jelly " + i, spec.x + spec.w * .5f, spec.y + spec.h * .5f, spec.w, spec.h);
                view.rect = root;

                view.glowImage = ChildImage(root, "Glow", JellyRunArt.Glow,
                    new Color(.41f, .95f, .84f, .10f), Vector2.zero,
                    new Vector2(spec.w * 1.12f, spec.h * 1.6f));

                view.shadow = ChildImage(root, "Shadow", JellyRunArt.Ellipse, new Color(0f, 0f, 0f, .28f),
                    new Vector2(0f, spec.h * .5f), new Vector2(spec.w * .9f, 18f));

                var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(CanvasRenderer));
                bodyGo.transform.SetParent(root, false);
                var bodyRt = (RectTransform)bodyGo.transform;
                bodyRt.anchorMin = Vector2.zero;
                bodyRt.anchorMax = Vector2.one;
                bodyRt.offsetMin = bodyRt.offsetMax = Vector2.zero;
                var body = bodyGo.AddComponent<JellyGraphic>();
                body.raycastTarget = false;
                body.blob = spec.blob;
                body.seed = i * 1.7f;
                view.body = body;
                view.bodyRect = bodyRt;
                jellies.Add(view);
            }
        }

        void BuildWellValve()
        {
            var valve = JellyRunData.Jellies[9].valve;
            wellValveGlow = WorldImage("Well Valve Glow", JellyRunArt.Glow, new Color(1f, .82f, .45f, .22f),
                valve.x, valve.y, 92f, 92f);
            wellValveRing = WorldImage("Well Valve Ring", JellyRunArt.Ring, new Color(1f, .82f, .45f, .75f),
                valve.x, valve.y, 42f, 42f);
            wellValveRingRect = wellValveRing.rectTransform;
            WorldImage("Well Valve Hub", JellyRunArt.Circle, new Color(1f, .90f, .62f, .95f),
                valve.x, valve.y, 13f, 13f);
            wellValveLabel = WorldText(null, "Well Valve Label", "阀门", 11,
                new Color(1f, .88f, .58f, .95f), TextAnchor.MiddleCenter, valve.x, valve.y - 28f, 100f, 18f);
            wellValveLabel.fontStyle = FontStyle.Bold;

            var shaftHint = WorldText(null, "Shaft Hint", "石墙顶可落脚 ↑", 11,
                new Color(.65f, 1f, .92f, .85f), TextAnchor.MiddleCenter, 4240f, 440f, 180f, 18f);
            shaftHint.fontStyle = FontStyle.Bold;
        }

        void BuildSwitch()
        {
            var s = JellyRunData.Switch;
            var root = WorldNode("Switch", s.position.x, s.position.y, 120f, 120f);
            switchView = new SwitchView { root = root };
            switchView.glow = ChildImage(root, "Glow", JellyRunArt.Glow,
                new Color(.51f, .92f, 1f, .26f), Vector2.zero, new Vector2(s.radius * 4.2f, s.radius * 4.2f));
            var crystal = ChildImage(root, "Crystal", JellyRunArt.Diamond,
                new Color(.55f, .96f, .92f, .9f), Vector2.zero, Vector2.one * (s.radius * 1.1f));
            crystal.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            switchView.crystalImage = crystal;
            switchView.crystal = crystal.rectTransform;
            var label = WorldText(null, "SwitchLabel", "机关", 12, new Color(.82f, 1f, .97f, .75f),
                TextAnchor.MiddleCenter, s.position.x, s.position.y - s.radius - 12f, 120f, 20f);
            label.fontStyle = FontStyle.Bold;
        }

        void BuildSaws()
        {
            for (int i = 0; i < JellyRunData.Saws.Length; i++)
            {
                var spec = JellyRunData.Saws[i];
                var view = new SawView { spec = spec, x = spec.ax, y = spec.ay, t = spec.phase };
                var root = WorldNode("Saw " + i, spec.ax, spec.ay, spec.r * 2f + 12f, spec.r * 2f + 12f);
                view.rect = root;
                view.glow = ChildImage(root, "Glow", JellyRunArt.Glow, new Color(1f, .35f, .35f, .18f),
                    Vector2.zero, Vector2.one * (spec.r * 3.2f));
                view.image = ChildImage(root, "Blade", i == 0 ? JellyRunArt.Saw : JellyRunArt.SawSmall,
                    Color.white, Vector2.zero, Vector2.one * (spec.r * 2f + 8f));
                view.image.preserveAspect = true;
                saws.Add(view);
            }
        }

        void BuildEntrances()
        {
            for (int i = 0; i < JellyRunData.Entrances.Length; i++)
            {
                var spec = JellyRunData.Entrances[i];
                var view = new EntranceView { index = i, spec = spec };

                view.beam = WorldImage("Entrance Beam " + i, JellyRunArt.Beam,
                    new Color(spec.accent.r / 255f, spec.accent.g / 255f, spec.accent.b / 255f, .38f),
                    spec.entryX, spec.top - 142f, 72f, 260f);
                view.glow = WorldImage("Entrance Glow " + i, JellyRunArt.Glow,
                    new Color(spec.accent.r / 255f, spec.accent.g / 255f, spec.accent.b / 255f, .30f),
                    spec.entryX, spec.top - 72f, 150f, 150f);
                view.ring = WorldImage("Entrance Ring " + i, JellyRunArt.Ring,
                    new Color(spec.accent.r / 255f, spec.accent.g / 255f, spec.accent.b / 255f, .85f),
                    spec.entryX, spec.top - 72f, 64f, 64f);
                view.pearl = WorldImage("Entrance Pearl " + i, JellyRunArt.Pearl, Color.white,
                    spec.entryX, spec.top - 72f, 32f, 32f);
                view.pearl.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

                for (int p = 0; p < 4; p++)
                {
                    var pip = WorldImage("Entrance " + i + " Pip " + p, JellyRunArt.Circle,
                        new Color(1f, 1f, 1f, .22f), spec.entryX - 30f + p * 20f, spec.top - 110f, 9f, 9f);
                    view.pips[p] = pip;
                }

                view.title = WorldText(null, "Entrance Title " + i, spec.Title, 14, Color.white,
                    TextAnchor.MiddleCenter, spec.entryX, spec.top - 156f, 280f, 22f);
                view.title.color = new Color(spec.accent.r / 255f, spec.accent.g / 255f, spec.accent.b / 255f, .95f);
                view.range = WorldText(null, "Entrance Range " + i, "关卡 " + spec.LevelRange, 11,
                    new Color(.75f, .86f, .86f, .8f), TextAnchor.MiddleCenter,
                    spec.entryX, spec.top - 134f, 280f, 20f);

                entrances.Add(view);
            }
        }

        void BuildPortal()
        {
            var p = JellyRunData.Portal;
            float cx = p.x + p.w * .5f, cy = p.y + p.h * .5f;
            var root = WorldNode("Portal", cx, cy, 240f, 240f);
            portal = new PortalView { root = root };
            portal.glow = ChildImage(root, "Glow", JellyRunArt.Glow,
                new Color(.47f, .59f, .67f, .26f), Vector2.zero, new Vector2(280f, 280f));
            for (int i = 0; i < 3; i++)
            {
                var arc = ChildImage(root, "Arc " + i, JellyRunArt.Arc(i * 120f, 100f),
                    new Color(.47f, .59f, .67f, .45f), Vector2.zero, Vector2.one * (110f + i * 26f));
                portal.arcs.Add(arc);
            }
            var outer = ChildImage(root, "OuterRing", JellyRunArt.Ring,
                new Color(.47f, .59f, .67f, .55f), Vector2.zero, new Vector2(116f, 116f));
            var inner = ChildImage(root, "InnerRing", JellyRunArt.Ring,
                new Color(.47f, .59f, .67f, .28f), Vector2.zero, new Vector2(100f, 100f));
            portal.rings.Add(outer);
            portal.rings.Add(inner);
            portal.core = ChildImage(root, "Core", JellyRunArt.Glow,
                new Color(.59f, .70f, .76f, .35f), Vector2.zero, new Vector2(80f, 80f));

            portal.count = WorldText(null, "Portal Count", "0 / 20", 15,
                new Color(.74f, .84f, .87f, .75f), TextAnchor.MiddleCenter, cx, p.y - 16f, 220f, 22f);
            portal.count.fontStyle = FontStyle.Bold;
            portal.state = WorldText(null, "Portal State", "封印中", 11,
                new Color(.63f, .75f, .78f, .65f), TextAnchor.MiddleCenter, cx, p.y - 1f, 220f, 18f);
        }

        void BuildFish()
        {
            fishView = new FishView
            {
                swimR = FishArt.Swim(FishFacing.Right),
                swimL = FishArt.Swim(FishFacing.Left),
                swimU = FishArt.Swim(FishFacing.Up),
                swimD = FishArt.Swim(FishFacing.Down),
                jumpR = FishArt.Jump(FishFacing.Right),
                jumpL = FishArt.Jump(FishFacing.Left),
                jumpU = FishArt.Jump(FishFacing.Up),
                jumpD = FishArt.Jump(FishFacing.Down),
                death = FishArt.Death(),
            };
            var root = WorldNode("Fish", fishPos.x, fishPos.y, 44f, 44f);
            fishView.root = root;
            fishView.glow = ChildImage(root, "Glow", JellyRunArt.Glow,
                new Color(.45f, .95f, .9f, .18f), Vector2.zero, new Vector2(64f, 64f));
            var imageGo = new GameObject("Sprite", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageGo.transform.SetParent(root, false);
            var imageRt = (RectTransform)imageGo.transform;
            imageRt.anchorMin = imageRt.anchorMax = new Vector2(.5f, .5f);
            imageRt.pivot = new Vector2(.5f, .5f);
            imageRt.sizeDelta = new Vector2(36f, 36f);
            imageRt.anchoredPosition = Vector2.zero;
            fishView.image = imageGo.GetComponent<Image>();
            fishView.image.preserveAspect = true;
            fishView.image.raycastTarget = false;
            fishView.imageRect = imageRt;

            var charge = ChildImage(root, "Charge", JellyRunArt.Ring, new Color(.55f, 1f, .92f, .8f),
                Vector2.zero, new Vector2(44f, 44f));
            fishView.charge = charge;
            fishView.chargeRect = charge.rectTransform;
            var hint = ChildImage(root, "DoubleJump", JellyRunArt.Ring, new Color(.59f, 1f, .92f, .6f),
                Vector2.zero, new Vector2(42f, 42f));
            fishView.hint = hint;
            fishView.hintRect = hint.rectTransform;
        }

        void BuildPools()
        {
            for (int i = 0; i < 140; i++)
            {
                var img = WorldImage("Particle " + i, JellyRunArt.Dot, Color.white, 0f, 0f, 4f, 4f);
                img.enabled = false;
                particles.Add(new Particle { rect = img.rectTransform, image = img });
            }
            for (int i = 0; i < 14; i++)
            {
                var text = WorldText(null, "Floater " + i, "", 15, new Color(1f, .91f, .64f, 1f),
                    TextAnchor.MiddleCenter, 0f, 0f, 180f, 24f);
                text.fontStyle = FontStyle.Bold;
                text.gameObject.SetActive(false);
                floaters.Add(new Floater { rect = text.rectTransform, text = text });
            }
        }

        // ---------------------------------------------------------------- helpers

        RectTransform WorldNode(string name, float cx, float cy, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(worldRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(cx, -cy);
            return rt;
        }

        Image WorldImage(string name, Sprite sprite, Color color, float cx, float cy, float w, float h)
        {
            var rt = WorldNode(name, cx, cy, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Image ChildImage(Transform parent, string name, Sprite sprite, Color color, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text WorldText(Transform parent, string name, string content, int size, Color color,
            TextAnchor align, float cx, float cy, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent != null ? parent : worldRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(cx, -cy);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        // ------------------------------------------------------------ presentation

        void UpdatePresentation(float dt)
        {
            if (worldRoot == null) return;
            float sx = shake > .4f ? UnityEngine.Random.Range(-shake, shake) * .5f : 0f;
            float sy = shake > .4f ? UnityEngine.Random.Range(-shake, shake) * .5f : 0f;
            worldRoot.anchoredPosition = new Vector2(-cam.x + sx, cam.y + sy);

            UpdateBackground();
            UpdateJellies(dt);
            UpdateWellValve();
            UpdateSaws();
            UpdateDoor();
            UpdateSwitch();
            UpdateEntrances(dt);
            UpdatePortal(dt);
            UpdateFishVisual(dt);
            UpdateParticles(dt);
            UpdateFloaters(dt);
            if (redFlash != null)
            {
                var c = redFlash.color;
                c.a = Mathf.Clamp01(flash) * .72f;
                redFlash.color = c;
            }
        }

        void UpdateBackground()
        {
            float cw = canvasRect != null && canvasRect.rect.width > 100f ? canvasRect.rect.width : JellyRunData.ViewW;
            float ch = canvasRect != null && canvasRect.rect.height > 100f ? canvasRect.rect.height : JellyRunData.ViewH;
            float caveW = cw / 1536f;
            float caveH = ch / 900f;
            if (caveFar != null)
            {
                caveFar.uvRect = new Rect(cam.x * .28f / 1536f, 1f - caveH, caveW, caveH);
                caveNear.uvRect = new Rect(cam.x * .52f / 1536f, 1f - caveH, caveW, caveH);
            }
            if (stalFar != null)
            {
                float sw = cw / 1536f;
                stalFar.uvRect = new Rect(cam.x * .34f / 1536f, 0f, sw, 1f);
                stalNear.uvRect = new Rect(cam.x * .52f / 1536f, 0f, sw, 1f);
            }
            for (int i = 0; i < motes.Count; i++)
            {
                float bx = Mathf.Repeat(i * 197f + cam.x * .86f, cw + 120f) - 60f;
                float by = Mathf.Repeat(i * 137f + time * (9f + i % 7 * 4f), ch + 80f) - 40f;
                motes[i].rectTransform.anchoredPosition = new Vector2(bx - cw * .5f, by - ch * .5f);
            }
        }

        void UpdateJellies(float dt)
        {
            for (int i = 0; i < jellies.Count; i++)
            {
                var j = jellies[i];
                if (j.y != j.spec.y || j.h != j.spec.h)
                    j.rect.anchoredPosition = new Vector2(j.x + j.w * .5f, -(j.y + j.h * .5f));
                j.rect.sizeDelta = new Vector2(j.w, j.h);
                j.body.time = time;
                j.body.squash = j.squash;
                j.body.glow = j.glow;
                j.body.SetVerticesDirty();
                j.glowImage.color = new Color(.41f, .95f, .84f, .07f + j.glow * .10f);
                j.shadow.rectTransform.anchoredPosition = new Vector2(0f, j.h * .5f);
                j.shadow.rectTransform.sizeDelta = new Vector2(j.w * .92f, 18f);

                for (int r = 0; r < j.ripples.Count; r++)
                {
                    var ripple = j.ripples[r];
                    float p = Mathf.Clamp01(ripple.t / 1.1f);
                    ripple.image.rectTransform.anchoredPosition = new Vector2(ripple.position.x, -j.y);
                    ripple.image.rectTransform.sizeDelta = new Vector2(16f + p * 104f, 6f + p * 24f);
                    var c = ripple.image.color;
                    c.a = (1f - p) * .7f;
                    ripple.image.color = c;
                }
            }
        }

        void UpdateSaws()
        {
            for (int i = 0; i < saws.Count; i++)
            {
                var s = saws[i];
                s.rect.anchoredPosition = new Vector2(s.x, -s.y);
                s.rect.localRotation = Quaternion.Euler(0f, 0f, s.angle);
            }
        }

        void UpdateDoor()
        {
            if (door == null) return;
            float h = JellyRunData.Door.h;
            door.bars.anchoredPosition = new Vector2(0f, door.open * (h + 30f));
        }

        void UpdateSwitch()
        {
            if (switchView == null) return;
            bool on = switchView.on;
            float pulse = .5f + .5f * Mathf.Sin(time * 2.4f + JellyRunData.Switch.position.x * .01f);
            switchView.glow.color = on
                ? new Color(1f, .78f, .42f, .42f)
                : new Color(.51f, .92f, 1f, .22f + pulse * .12f);
            switchView.crystalImage.color = on
                ? new Color(1f, .83f, .47f, .95f)
                : new Color(.55f, .96f, .92f, .55f + pulse * .2f);
            switchView.crystal.localScale = Vector3.one * (1f + pulse * .08f + switchView.pulse * .35f);
        }

        void UpdateWellValve()
        {
            if (wellValveRing == null) return;
            bool flooded = jellies.Count > 9 && jellies[9].flooded;
            float pulse = .5f + .5f * Mathf.Sin(time * 2.2f);
            wellValveGlow.color = flooded
                ? new Color(1f, .78f, .42f, .42f)
                : new Color(1f, .82f, .45f, .20f + pulse * .10f);
            wellValveRing.color = flooded
                ? new Color(1f, .88f, .58f, .95f)
                : new Color(1f, .82f, .45f, .62f + pulse * .18f);
            wellValveRingRect.localRotation = Quaternion.Euler(0f, 0f, time * (flooded ? 48f : 18f));
            wellValveLabel.text = flooded ? "潮水已开" : "阀门";
            wellValveLabel.color = flooded
                ? new Color(1f, .90f, .62f, .95f)
                : new Color(1f, .88f, .58f, .80f);
        }

        void UpdateEntrances(float dt)
        {
            int mask = GameSession.JellyRunCleared;
            for (int i = 0; i < entrances.Count; i++)
            {
                var e = entrances[i];
                float pulse = .5f + .5f * Mathf.Sin(time * 2f + i * 1.7f);
                bool complete = true;
                for (int p = 0; p < 4; p++)
                {
                    bool lit = (mask & (1 << (e.spec.FirstLevel + p))) != 0;
                    if (!lit) complete = false;
                    e.pips[p].color = lit
                        ? new Color(1f, .86f, .52f, .95f)
                        : new Color(1f, 1f, 1f, .20f);
                    e.pips[p].rectTransform.sizeDelta = Vector2.one * (lit ? 10f : 8f);
                }
                var accent = new Color(e.spec.accent.r / 255f, e.spec.accent.g / 255f, e.spec.accent.b / 255f, 1f);
                e.glow.color = new Color(accent.r, accent.g, accent.b, complete ? .38f : .22f + pulse * .08f);
                e.beam.color = new Color(accent.r, accent.g, accent.b, complete ? .55f : .30f + pulse * .08f);
                e.ring.color = new Color(accent.r, accent.g, accent.b, complete ? 1f : .72f);
                e.ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, time * (complete ? 26f : 12f));
                e.pearl.rectTransform.anchoredPosition = new Vector2(e.spec.entryX, -(e.spec.top - 72f + Mathf.Sin(time * 2f + i) * 4f));
                e.pearl.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f + Mathf.Sin(time * 1.4f + i) * 8f);
                e.title.color = new Color(accent.r, accent.g, accent.b, complete ? 1f : .85f);
            }
        }

        void UpdatePortal(float dt)
        {
            if (portal == null) return;
            int cleared = CountCleared(GameSession.JellyRunCleared);
            bool open = cleared >= 20;
            portal.open = open;
            Color col = open ? new Color(1f, .843f, .510f, 1f) : new Color(.470f, .588f, .667f, 1f);
            portal.glow.color = new Color(col.r, col.g, col.b, open ? .60f : .26f);
            portal.core.color = new Color(col.r, col.g, col.b, open ? .70f : .30f);
            for (int i = 0; i < portal.rings.Count; i++)
            {
                portal.rings[i].color = new Color(col.r, col.g, col.b, (open ? .95f : .55f) * (i == 0 ? 1f : .6f));
                portal.rings[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, time * (open ? 30f : 8f) * (i == 0 ? 1f : -1f));
            }
            for (int i = 0; i < portal.arcs.Count; i++)
            {
                portal.arcs[i].color = new Color(col.r, col.g, col.b, open ? .9f : .35f);
                portal.arcs[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, time * (open ? 90f : 25f) + i * 120f);
            }
            portal.count.text = cleared + " / 20";
            portal.state.text = open ? "已解锁" : "封印中";
            portal.count.color = open ? new Color(1f, .93f, .75f, .95f) : new Color(.74f, .84f, .87f, .75f);
            portal.state.color = open ? new Color(1f, .86f, .59f, .85f) : new Color(.63f, .75f, .78f, .65f);
        }

        void UpdateFishVisual(float dt)
        {
            if (fishView == null) return;
            fishView.root.anchoredPosition = new Vector2(fishPos.x, -fishPos.y);
            bool dead = phase == Phase.Dead;
            Sprite sprite = null;
            if (dead)
            {
                float p = Mathf.Clamp01(deadTimer / DeathSeconds);
                int frame = Mathf.Clamp(Mathf.FloorToInt(p * 6f), 0, 5);
                if (fishView.death != null && fishView.death.Length > frame) sprite = fishView.death[frame];
                fishView.image.color = new Color(1f, 1f, 1f, 1f - p * .15f);
                fishView.glow.color = new Color(.9f, .3f, .3f, .15f);
                fishView.root.localRotation = Quaternion.identity;
                fishView.root.localScale = Vector3.one;
            }
            else
            {
                animClock += dt;
                Sprite[] set = inJelly ? FramesForSwim(facing) : FramesForJump(facing);
                if (set != null && set.Length > 0)
                {
                    int frame = Mathf.FloorToInt(animClock / .11f) % set.Length;
                    sprite = set[Mathf.Max(0, frame)];
                }
                float speed = fishVel.magnitude;
                float tilt = Mathf.Clamp(Mathf.Atan2(fishVel.y, Mathf.Max(70f, Mathf.Abs(fishVel.x))) * .8f, -1f, 1f);
                fishView.root.localRotation = Quaternion.Euler(0f, 0f, -tilt * Mathf.Rad2Deg);
                float stretch = 1f + Mathf.Clamp(speed / 3200f, 0f, .18f);
                fishView.root.localScale = new Vector3(stretch, 1f / stretch, 1f);
                fishView.image.color = Color.white;
                float glowA = inJelly ? .18f : .12f;
                fishView.glow.color = new Color(.45f, .95f, .9f, glowA);
            }
            if (sprite != null) fishView.image.sprite = sprite;

            bool showCharge = !dead && inJelly && charge > .04f;
            fishView.charge.enabled = showCharge;
            if (showCharge)
            {
                fishView.charge.color = charge > .75f
                    ? new Color(1f, .9f, .59f, .95f)
                    : new Color(.55f, 1f, .92f, .85f);
                fishView.chargeRect.localScale = Vector3.one * (1f + charge * .4f);
                fishView.chargeRect.localRotation = Quaternion.Euler(0f, 0f, -90f + charge * 360f);
            }
            bool showHint = !dead && !inJelly && !usedDouble && !onGround && fishVel.y > -60f;
            fishView.hint.enabled = showHint;
            if (showHint)
            {
                fishView.hint.color = new Color(.59f, 1f, .92f, .55f + .25f * Mathf.Sin(time * 8f));
                fishView.hintRect.localScale = Vector3.one * (1f + .08f * Mathf.Sin(time * 8f));
            }
        }

        Sprite[] FramesForSwim(FishFacing f)
        {
            switch (f)
            {
                case FishFacing.Left: return fishView.swimL;
                case FishFacing.Up: return fishView.swimU;
                case FishFacing.Down: return fishView.swimD;
                default: return fishView.swimR;
            }
        }

        Sprite[] FramesForJump(FishFacing f)
        {
            switch (f)
            {
                case FishFacing.Left: return fishView.jumpL != null && fishView.jumpL.Length > 0 ? fishView.jumpL : fishView.swimL;
                case FishFacing.Up: return fishView.jumpU != null && fishView.jumpU.Length > 0 ? fishView.jumpU : fishView.swimU;
                case FishFacing.Down: return fishView.jumpD != null && fishView.jumpD.Length > 0 ? fishView.jumpD : fishView.swimD;
                default: return fishView.jumpR != null && fishView.jumpR.Length > 0 ? fishView.jumpR : fishView.swimR;
            }
        }

        // ---------------------------------------------------------------- effects

        void AddRipple(JellyView j, float x)
        {
            var img = WorldImage("Ripple", JellyRunArt.Ring, new Color(.82f, 1f, .97f, .7f),
                x, j.y, 16f, 8f);
            j.ripples.Add(new Ripple { image = img, position = new Vector2(x, j.y), t = 0f });
        }

        void RecycleRipple(Ripple ripple)
        {
            if (ripple.image != null) Destroy(ripple.image.gameObject);
            ripple.image = null;
        }

        void AddParticle(Vector2 pos, Vector2 vel, float life, Color color, float size, bool rise)
        {
            for (int i = 0; i < particles.Count; i++)
            {
                var p = particles[i];
                if (p.active) continue;
                p.active = true;
                p.pos = pos;
                p.vel = vel;
                p.t = 0f;
                p.life = life;
                p.color = color;
                p.size = size;
                p.rise = rise;
                p.image.enabled = true;
                p.rect.anchoredPosition = new Vector2(pos.x, -pos.y);
                p.rect.sizeDelta = Vector2.one * size;
                p.image.color = color;
                return;
            }
        }

        void Burst(float x, float y, int count, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                float a = UnityEngine.Random.value * Mathf.PI * 2f;
                float s = UnityEngine.Random.Range(50f, 190f);
                AddParticle(new Vector2(x, y), new Vector2(Mathf.Cos(a) * s, Mathf.Sin(a) * s),
                    UnityEngine.Random.Range(.4f, .9f), color, UnityEngine.Random.Range(1.6f, 3.6f), false);
            }
        }

        void Splash(float x, float y, float k)
        {
            int count = Mathf.RoundToInt(8f + k * 14f);
            for (int i = 0; i < count; i++)
            {
                float a = -Mathf.PI * .5f + UnityEngine.Random.Range(-1.1f, 1.1f);
                float s = UnityEngine.Random.Range(90f, 300f) * k;
                AddParticle(new Vector2(x, y), new Vector2(Mathf.Cos(a) * s, Mathf.Sin(a) * s),
                    UnityEngine.Random.Range(.4f, .8f), new Color(150 / 255f, 1f, 235 / 255f, 1f),
                    UnityEngine.Random.Range(1.4f, 3f), false);
            }
        }

        void Bubble(JellyView j, float x, float y)
        {
            AddParticle(new Vector2(x, y), new Vector2(UnityEngine.Random.Range(-12f, 12f), UnityEngine.Random.Range(-46f, -22f)),
                UnityEngine.Random.Range(.6f, 1.2f), new Color(190 / 255f, 1f, 245 / 255f, 1f),
                UnityEngine.Random.Range(1.2f, 2.4f), true);
        }

        void UpdateParticles(float dt)
        {
            for (int i = 0; i < particles.Count; i++)
            {
                var p = particles[i];
                if (!p.active) continue;
                p.t += dt;
                p.pos += p.vel * dt;
                if (p.rise) p.vel.y -= 18f * dt;
                else p.vel.y += 420f * dt;
                p.vel *= .985f;
                if (p.t >= p.life)
                {
                    p.active = false;
                    p.image.enabled = false;
                    continue;
                }
                float a = 1f - p.t / p.life;
                p.rect.anchoredPosition = new Vector2(p.pos.x, -p.pos.y);
                p.rect.sizeDelta = Vector2.one * (p.size * (.5f + a * .7f));
                var c = p.color;
                c.a *= a * .9f;
                p.image.color = c;
            }
        }

        void SpawnFloater(float x, float y, string text)
        {
            for (int i = 0; i < floaters.Count; i++)
            {
                var f = floaters[i];
                if (f.active) continue;
                f.active = true;
                f.t = 0f;
                f.life = 1f;
                f.text.gameObject.SetActive(true);
                f.text.text = text;
                f.rect.anchoredPosition = new Vector2(x, -y);
                return;
            }
        }

        void UpdateFloaters(float dt)
        {
            for (int i = 0; i < floaters.Count; i++)
            {
                var f = floaters[i];
                if (!f.active) continue;
                f.t += dt;
                f.rect.anchoredPosition += new Vector2(0f, 42f * dt);
                var c = f.text.color;
                c.a = Mathf.Clamp01(1f - f.t / f.life);
                f.text.color = c;
                if (f.t >= f.life)
                {
                    f.active = false;
                    f.text.gameObject.SetActive(false);
                }
            }
        }
    }
}
