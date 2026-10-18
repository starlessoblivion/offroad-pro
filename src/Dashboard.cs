using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OthDriveSystems
{
    // Third HUD dial, right of the pitch and roll indicators: a top-down drivetrain view.
    // Built from game objects and plain Images only (no injected classes).
    internal static class Dashboard
    {
        static readonly Color White = new(1f, 1f, 1f, 0.9f);
        static readonly Color Green = new(0.35f, 1f, 0.45f, 1f);
        static readonly Color Amber = new(1f, 0.72f, 0.12f, 1f);
        static readonly Color LockRed = new(1f, 0.35f, 0.2f, 1f);

        static IntPtr hudPtr;
        static GameObject dial;
        static RectTransform frontL, frontR, rearL, rearR;
        static Image wheelFL, wheelFR, wheelRL, wheelRR, shaftCenter, shaftFront, shaftRear, diffFront, diffCenter, diffRear;
        static TextMeshProUGUI label;
        static Sprite white;
        static string lastLabel;
        static readonly TextMeshProUGUI[] tileText = new TextMeshProUGUI[4];
        static readonly Mode[] tileMode = { Mode.Mud, Mode.Sand, Mode.Snow, Mode.Ford };   // 12, 3, 6, 9 o'clock
        static TextMeshProUGUI fontSource;
        static string lastTiles;
        static float circleRadius = 70f;
        static Vector2 circleCenter, carCenter;
        static bool haveCar;
        static Color terrainGrey = new(0.25f, 0.25f, 0.25f, 0.8f), skyGrey = new(0.45f, 0.45f, 0.45f, 0.8f);
        static readonly Image[] tileBack = new Image[4];
        static TextMeshProUGUI waypointText;
        static string lastWaypoint, lastWaypointState, lastPressure;
        static TextMeshProUGUI pressureText;
        static Image arrow;
        static System.IntPtr clearedPin;
        static float arrowX, arrowY;
        static readonly Color Orange = new(1f, 0.55f, 0.1f, 1f);

        public static void Update(VehicleState v)
        {
            if (!Plugin.Cfg.DashboardEnabled.Value) return;
            var hud = HUDManager.Instance;
            if (hud == null || hud.rollIndicator == null || hud.pitchIndicator == null) return;
            if (hud.Pointer != hudPtr || dial == null || dial.WasCollected) Build(hud);
            if (dial == null) return;

            bool show = v != null && hud.rollIndicator.gameObject.activeInHierarchy;
            if (dial.activeSelf != show) dial.SetActive(show);
            if (!show) return;

            Refresh(v);
        }

        static void Build(HUDManager hud)
        {
            hudPtr = hud.Pointer;
            var roll = hud.rollIndicator;
            var pitch = hud.pitchIndicator;
            dial = UnityEngine.Object.Instantiate(roll.gameObject, roll.transform.parent);
            dial.name = "OTHDS_DrivetrainDial";

            // Keep the frame; drop the incline behaviour and its rotating vehicle. Its vehicle image sets our drawing size.
            float vehicleH = 0f;
            var inc = dial.GetComponent<ContextUIInclineIndicator>();
            if (inc != null)
            {
                if (inc.rotatingPart != null)
                {
                    foreach (var img in inc.rotatingPart.GetComponentsInChildren<Image>(true))
                    {
                        if (img.gameObject.name == "Terrain") terrainGrey = img.color;
                        if (img.gameObject.name == "Sky") skyGrey = img.color;
                    }
                    foreach (var img in inc.rotatingPart.GetComponentsInChildren<Image>(true))
                        if (img.gameObject.name == "Car")
                        {
                            var crt = img.rectTransform;
                            vehicleH = Mathf.Max(crt.rect.width, crt.rect.height);
                            // The game's vehicle sits above the dial centre; put our drawing in the same spot.
                            var world = crt.TransformPoint(crt.rect.center);
                            var local = dial.transform.InverseTransformPoint(world);
                            // Local space is relative to the pivot; anchoredPosition here is relative to the rect centre.
                            var rc = dial.GetComponent<RectTransform>().rect.center;
                            carCenter = new Vector2(local.x - rc.x, local.y - rc.y);
                            haveCar = true;
                        }
                    inc.rotatingPart.gameObject.SetActive(false);
                }
                UnityEngine.Object.Destroy(inc);
            }
            LogHierarchy(dial.transform, 0);
            foreach (var img in dial.GetComponentsInChildren<Image>(true))
            {
                var n = img.gameObject.name.ToLowerInvariant();
                var sp = img.sprite != null ? img.sprite.name.ToLowerInvariant() : "";
                if (n == "lines" || n.Contains("tick") || n.Contains("mark") || n.Contains("grad") || n.Contains("notch") || sp.Contains("tick") || sp.Contains("grad"))
                    img.gameObject.SetActive(false);
            }

            var rt = dial.GetComponent<RectTransform>();
            var rollRt = roll.GetComponent<RectTransform>();
            var pitchRt = pitch.GetComponent<RectTransform>();
            rt.anchoredPosition = rollRt.anchoredPosition + (rollRt.anchoredPosition - pitchRt.anchoredPosition);

            if (white == null)
            {
                var tex = new Texture2D(4, 4);
                var px = new Color[16];
                for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                tex.SetPixels(px);
                tex.Apply();
                tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
                white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
                white.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }

            float dialSize = Mathf.Min(rt.rect.width, rt.rect.height);
            if (dialSize < 10f) dialSize = 100f;
            circleRadius = Mathf.Max(rt.rect.width, rt.rect.height) * 0.5f;
            circleCenter = new Vector2(0f, rt.rect.height * 0.5f - circleRadius);
            // Layout below is 0.78 s tall (wheelbase + tire), so match that to the game's vehicle image.
            float s = vehicleH > 1f ? vehicleH / 0.78f : dialSize * Plugin.Cfg.DashboardGraphicScale.Value;
            s = Mathf.Min(s, dialSize * 0.6f);
            BuildTiles(rt);
            var root = new GameObject("Drivetrain");
            var rootRt = root.AddComponent<RectTransform>();
            rootRt.SetParent(rt, false);
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.sizeDelta = new Vector2(s, s);
            rootRt.anchoredPosition = haveCar ? carCenter + new Vector2(0f, Plugin.Cfg.DashboardGraphicOffset.Value) : circleCenter;

            float tx = 0.27f * s, az = 0.27f * s;                  // half track, half wheelbase
            Vector2 wheelSize = new(0.12f * s, 0.24f * s);
            float shaft = 0.035f * s, diff = 0.085f * s;

            shaftCenter = Box(rootRt, "ShaftCenter", Vector2.zero, new Vector2(shaft, 2f * az));
            shaftFront = Box(rootRt, "ShaftFront", new Vector2(0, az), new Vector2(2f * tx, shaft));
            shaftRear = Box(rootRt, "ShaftRear", new Vector2(0, -az), new Vector2(2f * tx, shaft));
            diffFront = Box(rootRt, "DiffFront", new Vector2(0, az), new Vector2(diff, diff));
            diffCenter = Box(rootRt, "DiffCenter", Vector2.zero, new Vector2(diff, diff));
            diffRear = Box(rootRt, "DiffRear", new Vector2(0, -az), new Vector2(diff, diff));
            wheelFL = Box(rootRt, "WheelFL", new Vector2(-tx, az), wheelSize); frontL = wheelFL.rectTransform;
            wheelFR = Box(rootRt, "WheelFR", new Vector2(tx, az), wheelSize); frontR = wheelFR.rectTransform;
            wheelRL = Box(rootRt, "WheelRL", new Vector2(-tx, -az), wheelSize); rearL = wheelRL.rectTransform;
            wheelRR = Box(rootRt, "WheelRR", new Vector2(tx, -az), wheelSize); rearR = wheelRR.rectTransform;

            // Mode label under the dial, cloned from an existing HUD text so it uses the game's font.
            var srcText = hud.GetComponentInChildren<TextMeshProUGUI>(true);
            if (srcText != null)
            {
                var lgo = UnityEngine.Object.Instantiate(srcText.gameObject, rt);
                lgo.name = "OTHDS_ModeLabel";
                lgo.SetActive(true);
                label = lgo.GetComponent<TextMeshProUGUI>();
                var lrt = label.rectTransform;
                // Sits in the flat strip along the bottom edge of the dial.
                lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
                // Centred in a band along the flat bottom edge as thick as the mode-tile ring at the sides.
                float ts = Plugin.Cfg.DashboardTextSize.Value;
                float band = circleRadius - Mathf.Max(1f, circleRadius - Plugin.Cfg.DashboardTextInset.Value - ts * 0.4f);
                lrt.anchoredPosition = new Vector2(0f, -rt.rect.height * 0.5f + band * 0.5f + Plugin.Cfg.DashboardNameOffset.Value);
                lrt.sizeDelta = new Vector2(rt.rect.width, Plugin.Cfg.DashboardTextSize.Value * 1.6f);
                label.alignment = TextAlignmentOptions.Midline;   // centre on the letters, not the line box
                label.enableWordWrapping = false;
                label.fontSize = Plugin.Cfg.DashboardTextSize.Value;
                label.enableAutoSizing = false;
                label.fontStyle = FontStyles.Bold;
                label.color = White;
                lastLabel = null;
            }
            if (fontSource != null)
            {
                var wgo = UnityEngine.Object.Instantiate(fontSource.gameObject, rt);
                wgo.name = "OTHDS_Waypoint";
                wgo.SetActive(false);
                waypointText = wgo.GetComponent<TextMeshProUGUI>();
                var wrt = waypointText.rectTransform;
                wrt.anchorMin = wrt.anchorMax = wrt.pivot = new Vector2(0.5f, 0.5f);
                float drawingBottom = (haveCar ? carCenter.y : circleCenter.y) - s * 0.39f;   // drawing is 0.78 s tall
                float ts = Plugin.Cfg.DashboardTextSize.Value;
                float infoY = drawingBottom - ts * 0.9f;
                // Stacked under the drawing: waypoint distance (with direction wedge), then tire pressure.
                wrt.anchoredPosition = new Vector2(0f, infoY);
                wrt.sizeDelta = new Vector2(rt.rect.width * 0.8f, ts * 1.4f);
                waypointText.alignment = TextAlignmentOptions.Center;
                waypointText.enableWordWrapping = false;
                waypointText.enableAutoSizing = false;
                waypointText.fontStyle = FontStyles.Bold;
                waypointText.fontSize = Plugin.Cfg.DashboardTextSize.Value;
                waypointText.color = White;

                var pgo = UnityEngine.Object.Instantiate(wgo, rt);
                pgo.name = "OTHDS_Pressure";
                pgo.SetActive(true);
                pressureText = pgo.GetComponent<TextMeshProUGUI>();
                var prt = pressureText.rectTransform;
                prt.anchoredPosition = new Vector2(0f, infoY - ts * 1.25f);
                pressureText.alignment = TextAlignmentOptions.Center;
                lastPressure = null;

                // Orange wedge pointing at the waypoint, relative to the vehicle (nose up on the dial).
                var ago = new GameObject("OTHDS_WaypointArrow");
                var art = ago.AddComponent<RectTransform>();
                art.SetParent(rt, false);
                art.anchorMin = art.anchorMax = art.pivot = new Vector2(0.5f, 0.5f);
                art.sizeDelta = new Vector2(ts * 0.9f, ts * 1.1f);
                arrow = ago.AddComponent<Image>();
                arrow.sprite = WedgeSprite();
                arrow.color = Orange;
                arrow.raycastTarget = false;
                arrowY = infoY;
                ago.SetActive(false);
            }
            if (label != null) label.transform.SetAsLastSibling();   // system name draws above the mode tiles
            dial.SetActive(true);
            Plugin.L.LogInfo($"Dial greys: terrain={terrainGrey} sky={skyGrey}");
            Plugin.L.LogInfo($"Drivetrain dial added (dial {dialSize:F0}, car {vehicleH:F0} at {(haveCar ? carCenter.ToString() : "?")}, drawing {s:F0}, dial at {rt.anchoredPosition})");
        }

        static void LogHierarchy(Transform t, int depth)
        {
            var rt = t.GetComponent<RectTransform>();
            var img = t.GetComponent<Image>();
            Plugin.L.LogInfo($"[dial] {new string(' ', depth * 2)}{t.gameObject.name} size={(rt != null ? rt.rect.size.ToString() : "-")} sprite={(img != null && img.sprite != null ? img.sprite.name : "-")} active={t.gameObject.activeSelf}");
            for (int i = 0; i < t.childCount; i++) LogHierarchy(t.GetChild(i), depth + 1);
        }

        // Four mode labels bent along the curved rim (watch-bezel style); the flat bottom edge is left clear.
        static void BuildTiles(RectTransform parent)
        {
            BuildHighlights(parent);
            if (fontSource == null) fontSource = HUDManager.Instance?.GetComponentInChildren<TextMeshProUGUI>(true);
            if (fontSource == null) return;
            for (int i = 0; i < 4; i++)
            {
                var tgo = UnityEngine.Object.Instantiate(fontSource.gameObject, parent);
                tgo.name = $"OTHDS_ModeText{i}";
                tgo.SetActive(true);
                var txt = tgo.GetComponent<TextMeshProUGUI>();
                var r = txt.rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = circleCenter;
                r.localEulerAngles = Vector3.zero;
                r.sizeDelta = new Vector2(circleRadius * 4f, circleRadius);
                txt.alignment = TextAlignmentOptions.Center;
                txt.enableWordWrapping = false;
                txt.enableAutoSizing = false;
                txt.fontStyle = FontStyles.Bold;
                txt.color = White;
                tileText[i] = txt;
            }
            lastTiles = null;
        }

        // Curved highlight behind the active mode label, in the pitch indicator's grey.
        static void BuildHighlights(RectTransform parent)
        {
            float baseline = circleRadius - Plugin.Cfg.DashboardTextInset.Value;
            float size = Plugin.Cfg.DashboardTextSize.Value;
            float outer = circleRadius;                                 // tile runs to the dial's outer edge
            float inner = Mathf.Max(1f, baseline - size * 0.4f);
            var ringSprite = RingSprite(inner / outer);
            for (int i = 0; i < 4; i++)
            {
                var (centre, span) = TileRange(i);
                var go = new GameObject($"OTHDS_ModeTile{i}");
                var trt = go.AddComponent<RectTransform>();
                trt.SetParent(parent, false);
                trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
                trt.sizeDelta = new Vector2(outer * 2f, outer * 2f);
                trt.anchoredPosition = circleCenter;
                trt.localEulerAngles = new Vector3(0, 0, -(centre - span / 2f));
                var img = go.AddComponent<Image>();
                img.sprite = ringSprite;
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Radial360;
                img.fillOrigin = (int)Image.Origin360.Top;
                img.fillClockwise = true;
                img.fillAmount = span / 360f;
                img.color = Plugin.Cfg.DashboardHighlightUseSky.Value ? skyGrey : terrainGrey;
                img.raycastTarget = false;
                go.SetActive(false);
                tileBack[i] = img;
            }
        }

        static Sprite RingSprite(float innerRatio)
        {
            const int n = 256;
            var tex = new Texture2D(n, n);
            var px = new Color[n * n];
            float c = (n - 1) / 2f, outerR = c, innerR = c * innerRatio;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(outerR - r) * Mathf.Clamp01(r - innerR));
                }
            tex.SetPixels(px);
            tex.Apply();
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            s.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return s;
        }

        // Tile i (0 Mud, 1 Sand, 2 Snow, 3 Ford) as (centre, span) in degrees clockwise from 12 o'clock.
        // Sand/Snow fill 0..split either side of the top; Mud/Ford run from split down to outerEnd near the flat bottom.
        static (float centre, float span) TileRange(int i)
        {
            const float gap = 4f;
            float split = Plugin.Cfg.DashboardTileSplit.Value, outer = Plugin.Cfg.DashboardTileOuterEnd.Value;
            float a0, a1;
            switch (i)
            {
                case 0: a0 = -outer; a1 = -split; break;
                case 1: a0 = -split; a1 = 0f; break;
                case 2: a0 = 0f; a1 = split; break;
                default: a0 = split; a1 = outer; break;
            }
            return ((a0 + a1) * 0.5f, a1 - a0 - gap);
        }

        // Bends each label onto an arc just inside the rim: glyphs upright, facing outward, reading clockwise.
        static void WarpTiles()
        {
            float radius = circleRadius - Plugin.Cfg.DashboardTextInset.Value;
            for (int i = 0; i < 4; i++)
            {
                var txt = tileText[i];
                if (txt == null) continue;
                var (centre, _) = TileRange(i);
                txt.ForceMeshUpdate();
                var ti = txt.textInfo;
                if (ti.characterCount == 0) continue;
                for (int c = 0; c < ti.characterCount; c++)
                {
                    var ci = ti.characterInfo[c];
                    if (!ci.isVisible) continue;
                    var verts = ti.meshInfo[ci.materialReferenceIndex].vertices;
                    int vi = ci.vertexIndex;
                    float midX = (verts[vi].x + verts[vi + 2].x) * 0.5f;
                    float a = (centre + midX / radius * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                    var outward = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                    var tangent = new Vector2(Mathf.Cos(a), -Mathf.Sin(a));
                    for (int k = 0; k < 4; k++)
                    {
                        var v = verts[vi + k];
                        float ox = v.x - midX, oy = v.y - ci.baseLine;
                        var p = outward * radius + tangent * ox + outward * oy;
                        verts[vi + k] = new Vector3(p.x, p.y, v.z);
                    }
                }
                txt.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            }
        }

        static Sprite wedge;

        // Isosceles wedge pointing up, generated once.
        static Sprite WedgeSprite()
        {
            if (wedge != null) return wedge;
            const int n = 64;
            var tex = new Texture2D(n, n);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                float half = (n - 1 - y) * 0.5f;                     // full width at the bottom, point at the top
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Abs(x - (n - 1) / 2f);
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(half - dx + 0.5f));
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            wedge = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            wedge.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return wedge;
        }

        // Distance and compass bearing (north = world +Z) from the player's vehicle to their own map waypoint flare.
        static void UpdateWaypoint(VehicleState v)
        {
            if (waypointText == null) return;
            Transform pin = FindCompassPin(v);
            bool show = pin != null;
            if (arrow != null && arrow.gameObject.activeSelf != show) arrow.gameObject.SetActive(show);

            if (waypointText.gameObject.activeSelf != show) waypointText.gameObject.SetActive(show);
            if (!show) return;

            var from = v.Vc.transform.position;
            var to = pin.position;
            var delta = new Vector2(to.x - from.x, to.z - from.z);
            float dist = delta.magnitude;
            if (dist < Plugin.Cfg.WaypointClearDistance.Value && pin.Pointer != clearedPin)
            {
                // Arrived: remove the player's own pin the same way the map does.
                clearedPin = pin.Pointer;
                try
                {
                    var runner = v.Input != null ? v.Input.Runner : null;
                    var map = MapNetworkManager.Instance;
                    if (runner != null && map != null)
                    {
                        map.RPC_DestroyPinpoint(runner.LocalPlayer);
                        Plugin.L.LogInfo($"waypoint reached ({dist:F1} m), cleared");
                    }
                }
                catch (System.Exception e) { Plugin.L.LogWarning($"waypoint clear failed: {e.Message}"); }
            }
            float bearing = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;          // world, north = +Z
            var fwd = v.Vc.transform.forward;
            float heading = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            float relative = Mathf.DeltaAngle(heading, bearing);                     // + = waypoint to the right
            var text = Units.Distance(dist);
            if (text != lastWaypoint)
            {
                waypointText.text = text;
                lastWaypoint = text;
                arrowX = -waypointText.GetPreferredValues(text).x * 0.5f - Plugin.Cfg.DashboardTextSize.Value * 0.8f;
            }
            if (arrow != null)
            {
                arrow.rectTransform.anchoredPosition = new Vector2(arrowX, arrowY);
                arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, -relative);
            }
        }

        // The waypoint as the compass shows it: a visible pinpoint icon on the compass bar, preferring the local player's pin.
        static Transform FindCompassPin(VehicleState v)
        {
            Transform best = null, any = null;
            var log = new System.Text.StringBuilder();
            try
            {
                var compass = HUDManager.Instance != null ? HUDManager.Instance.CompassController : null;
                var list = compass != null ? compass.trackedPinpoints : null;
                if (list == null) { LogPins("no compass list"); return null; }
                Fusion.PlayerRef local = default;
                bool haveLocal = false;
                try { if (v.Input != null && v.Input.Runner != null) { local = v.Input.Runner.LocalPlayer; haveLocal = true; } } catch { }
                for (int i = 0; i < list.Count; i++)
                {
                    var icon = list[i];
                    if (icon == null) continue;
                    var t = icon.Target;
                    bool visible = icon.gameObject.activeInHierarchy && t != null;
                    log.Append($"[{i} vis={visible} {(t != null ? t.position.ToString("F0") : "-")}] ");
                    if (!visible) continue;
                    any ??= t;
                    if (haveLocal)
                    {
                        var wsp = t.GetComponentInParent<WorldSpacePinpoint>();
                        try { if (wsp != null && wsp.networkObject != null && wsp.networkObject.InputAuthority == local) best = t; } catch { }
                    }
                }
            }
            catch (System.Exception e) { LogPins("error " + e.Message); return null; }
            LogPins($"{log}");
            return best ?? any;
        }

        static void LogPins(string state)
        {
            if (state == lastWaypointState) return;
            lastWaypointState = state;
            Plugin.L.LogInfo($"compass pins: {state}");
        }

        static Image Box(RectTransform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.sprite = white;
            img.color = White;
            img.raycastTarget = false;
            return img;
        }

        static void Refresh(VehicleState v)
        {
            // Wheel angles from the physics wheel states, matched to corners by local position.
            float fl = 0, fr = 0, rl = 0, rr = 0;
            var states = v.Vc.m_wheelState;
            for (int i = 0; i < states.Length; i++)
            {
                var ws = states[i];
                var p = v.Vc.transform.InverseTransformPoint(ws.wheelCol.transform.position);
                bool front = p.z > 0f, left = p.x < 0f;
                float a = ws.steeringAngle;
                if (front) { if (left) fl = a; else fr = a; }
                else { if (left) rl = a; else rr = a; }
            }
            frontL.localEulerAngles = new Vector3(0, 0, -fl);
            frontR.localEulerAngles = new Vector3(0, 0, -fr);
            rearL.localEulerAngles = new Vector3(0, 0, -rl);
            rearR.localEulerAngles = new Vector3(0, 0, -rr);

            // Tires widen on the dial as they are aired down.
            float wide = 1f + 0.6f * v.Deflation;
            foreach (var w in new[] { frontL, frontR, rearL, rearR }) w.localScale = new Vector3(wide, 1f, 1f);
            wheelFL.color = wheelFR.color = Green;
            wheelRL.color = wheelRR.color = v.RearSteerOn ? Green : White;

            bool low = v.IsLowRange;
            shaftCenter.color = shaftFront.color = shaftRear.color = low ? Amber : White;

            bool showDiffs = Plugin.Cfg.DashboardDiffMarkers.Value;
            var phys = DriveModes.Physics[(int)v.Mode];
            var axles = v.TankActive ? DiffLock.Open : phys.Axles;
            var center = v.TankActive ? DiffLock.Stock : phys.Center;
            diffFront.gameObject.SetActive(showDiffs);
            diffCenter.gameObject.SetActive(showDiffs);
            diffRear.gameObject.SetActive(showDiffs);
            diffFront.color = diffRear.color = axles == DiffLock.Locked ? LockRed : White;
            diffCenter.color = center == DiffLock.Locked ? LockRed : White;

            var tiles = $"{v.System.Name}|{v.Mode}";
            if (tiles != lastTiles)
            {
                lastTiles = tiles;
                for (int i = 0; i < 4; i++)
                {
                    var txt = tileText[i];
                    if (txt == null) continue;
                    txt.text = v.System.Label(tileMode[i]);
                    txt.color = v.Mode == tileMode[i] ? Green : White;
                    if (tileBack[i] != null) tileBack[i].gameObject.SetActive(v.Mode == tileMode[i]);
                    txt.fontSize = Plugin.Cfg.DashboardTextSize.Value;   // one size for every label
                }
            }
            WarpTiles();

            UpdateWaypoint(v);
            if (pressureText != null)
            {
                bool showP = Plugin.Cfg.CtisEnabled.Value && v.Psi >= 0f;
                if (pressureText.gameObject.activeSelf != showP) pressureText.gameObject.SetActive(showP);
                var ptxt = showP ? Units.Pressure(v.Psi) : "";
                if (ptxt != lastPressure) { pressureText.text = ptxt; lastPressure = ptxt; }
            }

            if (label != null)
            {
                var text = v.System.Name;
                if (text != lastLabel)
                {
                    lastLabel = text;
                    // Fall back to the short name if the full one is wider than the flat bottom strip.
                    float room = label.rectTransform.rect.width * 0.72f;
                    label.text = label.GetPreferredValues(text).x > room ? v.System.Short : text;
                }
            }
        }
    }
}
