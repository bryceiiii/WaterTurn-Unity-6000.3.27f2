using UnityEngine;

namespace WaterTurn
{
    /// <summary>
    /// Scene-authoring support: binds the authored prefab instances under a
    /// JellyRunLevelBoard to the runtime simulation.  The authored scene owns
    /// positions and sizes; the HTML tables are only the initial bake source.
    /// </summary>
    public sealed partial class JellyRunGame
    {
        void BindSceneWorld(JellyRunLevelBoard board)
        {
            jellies.Clear();
            saws.Clear();
            entrances.Clear();
            floorBoxes.Clear();
            entrancePlatformBoxes.Clear();
            spikeBoxes.Clear();
            valves.Clear();
            door = null;
            switchView = null;
            portal = null;
            wellValveTarget = null;

            if (worldRoot == null) worldRoot = (RectTransform)board.transform;

            foreach (var view in board.GetComponentsInChildren<JellyRunFloorView>(true))
            {
                view.Refresh();
                floorBoxes.Add(JellyRunUi.GameRect(worldRoot, view.Rect));
            }

            foreach (var view in board.GetComponentsInChildren<JellyRunLedgeView>(true))
            {
                view.Refresh();
                entrancePlatformBoxes.Add(JellyRunUi.GameRect(worldRoot, view.Rect));
            }

            foreach (var view in board.GetComponentsInChildren<JellyRunSpikeView>(true))
            {
                view.Refresh();
                var r = JellyRunUi.GameRect(worldRoot, view.Rect);
                spikeBoxes.Add(new JellyRunData.SpikeF(r.x, r.y, r.w, r.h, view.up));
            }

            foreach (var view in board.GetComponentsInChildren<JellyRunJellyView>(true))
            {
                view.Refresh();
                var r = JellyRunUi.GameRect(worldRoot, view.Rect);
                var spec = new JellyRunData.JellyF(r.x, r.y, r.w, r.h, view.blob, view.floodTo, Vector2.zero);
                jellies.Add(new JellyView
                {
                    index = jellies.Count,
                    spec = spec,
                    x = r.x, y = r.y, w = r.w, h = r.h,
                    rect = view.Rect,
                    body = view.body,
                    glowImage = view.glow,
                    shadow = view.shadow,
                    seed = jellies.Count * 1.7f,
                });
            }

            foreach (var view in board.GetComponentsInChildren<JellyRunSawView>(true))
            {
                view.Refresh();
                if (view.pathA == null || view.pathB == null) continue;
                Vector2 a = JellyRunUi.GamePoint(worldRoot, view.pathA.position);
                Vector2 b = JellyRunUi.GamePoint(worldRoot, view.pathB.position);
                saws.Add(new SawView
                {
                    spec = new JellyRunData.SawF(a.x, a.y, b.x, b.y, view.radius, view.speed, view.phase),
                    rect = view.Rect,
                    image = view.blade,
                    glow = view.glow,
                    t = view.phase,
                    x = a.x,
                    y = a.y,
                });
            }

            var doorView = board.GetComponentInChildren<JellyRunDoorView>(true);
            if (doorView != null)
            {
                doorView.Refresh();
                door = new DoorView { rect = doorView.Rect, bars = doorView.bars, frame = doorView.frame };
            }

            var switchViewSrc = board.GetComponentInChildren<JellyRunSwitchView>(true);
            if (switchViewSrc != null)
            {
                switchViewSrc.Refresh();
                switchView = new SwitchView
                {
                    root = switchViewSrc.transform as RectTransform,
                    crystal = switchViewSrc.crystal.rectTransform,
                    glow = switchViewSrc.glow,
                    crystalImage = switchViewSrc.crystal,
                };
                switchPosition = JellyRunUi.GamePoint(worldRoot, switchViewSrc.transform.position);
                switchRadius = Mathf.Max(8f, switchViewSrc.radius);
            }

            var portalView = board.GetComponentInChildren<JellyRunPortalView>(true);
            if (portalView != null)
            {
                portalView.Refresh();
                portalBox = JellyRunUi.GameRect(worldRoot, portalView.trigger);
                portalCenter = new Vector2(portalBox.x + portalBox.w * .5f, portalBox.y + portalBox.h * .5f);
                portal = new PortalView
                {
                    root = portalView.transform as RectTransform,
                    glow = portalView.glow,
                    core = portalView.core,
                    count = portalView.count,
                    state = portalView.state,
                };
                portal.rings.AddRange(portalView.rings);
                portal.arcs.AddRange(portalView.arcs);
            }

            foreach (var view in board.GetComponentsInChildren<JellyRunEntranceView>(true))
            {
                view.Refresh();
                int chapter = Mathf.Clamp(view.chapter, 0, 4);
                var baseData = JellyRunData.Entrances[chapter];
                var platformRect = view.platform != null
                    ? JellyRunUi.GameRect(worldRoot, view.platform)
                    : JellyRunUi.GameRect(worldRoot, view.Rect);
                Vector2 entry = JellyRunUi.GamePoint(worldRoot, view.Rect.position);
                var spec = new JellyRunData.Entrance
                {
                    chapter = chapter,
                    zone = baseData.zone,
                    entryX = entry.x,
                    top = entry.y,
                    platformX = platformRect.x,
                    platformW = platformRect.w,
                    accent = baseData.accent,
                };
                var runtime = new EntranceView
                {
                    index = entrances.Count,
                    spec = spec,
                    root = view.Rect,
                    pearlRect = view.pearl != null ? view.pearl.rectTransform : null,
                    glow = view.glow,
                    ring = view.ring,
                    pearl = view.pearl,
                    beam = view.beam,
                    title = view.title,
                    range = view.range,
                    pearlBase = view.pearl != null ? view.pearl.rectTransform.anchoredPosition : Vector2.zero,
                };
                for (int i = 0; i < 4 && i < view.pips.Length; i++) runtime.pips[i] = view.pips[i];
                entrances.Add(runtime);
            }

            foreach (var view in board.GetComponentsInChildren<JellyRunValveView>(true))
            {
                view.Refresh();
                JellyView target = null;
                if (view.target != null)
                {
                    for (int i = 0; i < jellies.Count; i++)
                        if (jellies[i].rect == view.target.Rect) { target = jellies[i]; break; }
                }
                if (target == null)
                {
                    Vector2 point = JellyRunUi.GamePoint(worldRoot, view.transform.position);
                    float best = float.MaxValue;
                    for (int i = 0; i < jellies.Count; i++)
                    {
                        if (jellies[i].spec.floodTo < 0f) continue;
                        float d = Vector2.Distance(point, new Vector2(jellies[i].x + jellies[i].w * .5f,
                            jellies[i].y + jellies[i].h * .5f));
                        if (d < best) { best = d; target = jellies[i]; }
                    }
                }
                if (target == null) continue;
                var binding = new ValveBinding
                {
                    target = target,
                    position = JellyRunUi.GamePoint(worldRoot, view.transform.position),
                    radius = Mathf.Max(20f, view.radius),
                    view = view,
                };
                valves.Add(binding);
                if (wellValveTarget == null)
                {
                    wellValveTarget = target;
                    wellValveGlow = view.glow;
                    wellValveRing = view.ring;
                    wellValveRingRect = view.ring != null ? view.ring.rectTransform : null;
                    wellValveLabel = view.label;
                }
            }

            var spawn = board.GetComponentInChildren<JellyRunSpawn>(true);
            spawnJelly = 0;
            if (spawn != null)
            {
                spawnPoint = JellyRunUi.GamePoint(worldRoot, spawn.transform.position);
            }
            else if (jellies.Count > 0)
            {
                spawnPoint = new Vector2(jellies[0].x + jellies[0].w * .5f, jellies[0].y + jellies[0].h * .5f);
            }

            if (jellies.Count > 0)
            {
                float best = float.MaxValue;
                for (int i = 0; i < jellies.Count; i++)
                {
                    var j = jellies[i];
                    if (j.Box.Contains(spawnPoint.x, spawnPoint.y, 4f)) { spawnJelly = i; best = 0f; break; }
                    float d = Vector2.Distance(spawnPoint, new Vector2(j.x + j.w * .5f, j.y + j.h * .5f));
                    if (d < best) { best = d; spawnJelly = i; }
                }
            }

            if (floorBoxes.Count == 0 || jellies.Count == 0 || entrances.Count == 0)
                Debug.LogError("WaterTurn: JellyRunLevelBoard 场景缺少 Floor / Jelly / Entrance，运行可能不完整。");

            BuildFish();
            BuildPools();
        }
    }
}
