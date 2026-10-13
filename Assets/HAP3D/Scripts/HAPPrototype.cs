using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HAP3D
{
    /// <summary>Three short, locally controlled HAP puzzles. Timing and camera choices are experimental.</summary>
    public sealed class HAPPrototype : MonoBehaviour
    {
        private HAPStageDefinition[] stages;
        private HAPActor yang;
        private HAPActor yin;
        private readonly List<HAPLightBridge> bridges = new List<HAPLightBridge>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private Camera gameCamera;
        private Transform goalHalo;
        private Vector2 moveInput;
        private bool automatedControl;
        private bool won;
        private int selectedIndex;
        private float visualClock;
        private float victoryTimer;
        private int tutorialKeyMask;
        private float tutorialFadeTime;
        private Font font;
        private GUIStyle stageLabelStyle, keyLabelStyle;
        private Texture2D roundedKeyMask;
        private static readonly Color Ink = new Color(.16f, .22f, .25f);
        private static readonly Color KeyFill = new Color(.96f, .95f, .91f, .94f);
        private static readonly Color Teal = new Color(.20f, .55f, .51f);
        private const int AllTutorialKeys = (1 << 7) - 1;
        private const float TutorialFadeDuration = .25f;

        public Rigidbody YangBody => yang == null ? null : yang.Body;
        public Rigidbody YinBody => yin == null ? null : yin.Body;
        public BoxCollider LightCollider => bridges.Count == 0 ? null : bridges[0].Collider;
        public string LightState => bridges.Count == 0 ? "None" : bridges[0].State.ToString();
        public bool GoalReached => won;
        public int SelectedIndex => selectedIndex;
        public bool YangGrounded => yang != null && yang.Grounded;
        public bool YinGrounded => yin != null && yin.Grounded;
        public int YangGravitySign => yang == null ? -1 : yang.GravitySign;
        public int YinGravitySign => yin == null ? -1 : yin.GravitySign;
        public int StageIndex { get; private set; }
        public int StageCount => 3;
        public bool IsTitleScreen => false;
        public IReadOnlyList<HAPLightBridge> Bridges => bridges.AsReadOnly();
        public HAPStageDefinition CurrentStage { get; private set; }
        public Transform StageRoot { get; private set; }

        private void Start()
        {
            if (CurrentStage != null) return;
            LoadStage(0);
        }

        private void Update()
        {
            if (CurrentStage == null) return;
            if (!automatedControl && StageIndex == 0) TrackTutorialKeys();
            else if (tutorialKeyMask == AllTutorialKeys && tutorialFadeTime > 0f)
                tutorialFadeTime = Mathf.Max(0f, tutorialFadeTime - Time.unscaledDeltaTime);
            if (automatedControl) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (!Application.isEditor) Application.Quit();
                return;
            }
            bool advanceKey = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
            if (won && StageIndex == StageCount - 1 &&
                (Input.GetKeyDown(KeyCode.R) || advanceKey))
            {
                StartGame();
                return;
            }
            if (Input.GetKeyDown(KeyCode.R)) { RestartPrototype(); return; }
            if (won)
            {
                if (advanceKey)
                {
                    AdvanceStage();
                    return;
                }
                if (StageIndex < StageCount - 1 && (victoryTimer += Time.deltaTime) >= .9f)
                    AdvanceStage();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Tab)) SelectCharacter(1 - selectedIndex);
            if (Input.GetKeyDown(KeyCode.G)) TryFlipSelectedGravity();
            SetMoveInput(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")));
        }

        private void FixedUpdate()
        {
            if (yang == null || yin == null) return;
            yang.RefreshGrounded(); yin.RefreshGrounded();
            if (won) return;
            yang.Simulate(selectedIndex == 0 ? moveInput : Vector2.zero, false);
            yin.Simulate(selectedIndex == 1 ? moveInput : Vector2.zero, false);
            foreach (var bridge in bridges) bridge.Tick(yang, yin, Time.fixedDeltaTime);
            if (!CurrentStage.PlayBounds.Contains(yang.Body.position) || !CurrentStage.PlayBounds.Contains(yin.Body.position))
            {
                RestartPrototype();
                return;
            }
            if (yang.Grounded && yin.Grounded && yang.GravitySign == CurrentStage.GoalGravitySign &&
                yin.GravitySign == CurrentStage.GoalGravitySign && CurrentStage.GoalBounds.Contains(yang.Body.position) &&
                CurrentStage.GoalBounds.Contains(yin.Body.position))
            {
                won = true; moveInput = Vector2.zero;
                yang.Body.linearVelocity = Vector3.zero; yin.Body.linearVelocity = Vector3.zero;
                yang.Body.isKinematic = true; yin.Body.isKinematic = true;
            }
        }

        private void LateUpdate()
        {
            if (yang == null) return;
            visualClock += Time.deltaTime;
            yang.Animate(selectedIndex == 0, won, Time.deltaTime);
            yin.Animate(selectedIndex == 1, won, Time.deltaTime);
            if (goalHalo != null)
            {
                float scale = 1f + .035f * Mathf.Sin(visualClock * 2f);
                goalHalo.localScale = new Vector3(scale, 1, scale);
            }
        }

        public void SetAutomatedControl(bool enabled)
        { automatedControl = enabled; if (enabled) moveInput = Vector2.zero; }
        /// <summary>World X/Z movement, independent of the fixed camera.</summary>
        public void SetMoveInput(Vector2 input) => moveInput = Vector2.ClampMagnitude(input, 1f);
        public void SelectCharacter(int index) => selectedIndex = index == 0 ? 0 : 1;

        public bool TryFlipSelectedGravity()
        {
            if (won || yang == null) return false;
            var actor = selectedIndex == 0 ? yang : yin;
            return actor.TryFlip();
        }

        public void StartGame()
        {
            tutorialKeyMask = 0;
            tutorialFadeTime = 0f;
            LoadStage(0);
        }

        private void TrackTutorialKeys()
        {
            if (tutorialKeyMask == AllTutorialKeys)
            {
                tutorialFadeTime = Mathf.Max(0f, tutorialFadeTime - Time.unscaledDeltaTime);
                return;
            }

            int previousMask = tutorialKeyMask;
            RecordTutorialKey(KeyCode.W, 1 << 0);
            RecordTutorialKey(KeyCode.A, 1 << 1);
            RecordTutorialKey(KeyCode.S, 1 << 2);
            RecordTutorialKey(KeyCode.D, 1 << 3);
            RecordTutorialKey(KeyCode.Tab, 1 << 4);
            RecordTutorialKey(KeyCode.G, 1 << 5);
            RecordTutorialKey(KeyCode.R, 1 << 6);
            if (previousMask != AllTutorialKeys && tutorialKeyMask == AllTutorialKeys)
                tutorialFadeTime = TutorialFadeDuration;
        }

        private void RecordTutorialKey(KeyCode key, int bit)
        {
            if (Input.GetKeyDown(key)) tutorialKeyMask |= bit;
        }

        public void LoadStage(int index)
        {
            if (index < 0 || index >= StageCount) throw new ArgumentOutOfRangeException(nameof(index));
            if (stages == null) stages = HAPStageDefinition.CreateStages();
            ClearStage();
            StageIndex = index; CurrentStage = stages[index];
            selectedIndex = 0; moveInput = Vector2.zero; won = false;
            visualClock = 0; victoryTimer = 0;
            StageRoot = new GameObject("HAP stage " + (index + 1) + " — " + CurrentStage.Name).transform;
            StageRoot.SetParent(transform, false);
            BuildStage();
            Physics.SyncTransforms();
        }

        public void AdvanceStage()
        {
            if (!won || StageIndex + 1 >= StageCount) return;
            LoadStage(StageIndex + 1);
        }

        public void RestartPrototype() => LoadStage(StageIndex);

        private void BuildStage()
        {
            var warm = MakeMaterial("Warm ceramic", new Color(.84f, .83f, .76f));
            var pale = MakeMaterial("Chalk top", new Color(.96f, .95f, .88f));
            var slate = MakeMaterial("Slate base", new Color(.32f, .39f, .42f));
            var shadow = MakeMaterial("Shadow stone", new Color(.24f, .31f, .34f));
            var accent = MakeMaterial("Jade accents", new Color(.30f, .68f, .61f), .10f);
            var ivory = MakeMaterial("Yang ivory", new Color(.98f, .91f, .72f));
            var dark = MakeMaterial("Yin ink", new Color(.13f, .21f, .26f));
            foreach (var solid in CurrentStage.Solids) BuildIsland(solid, pale, warm, slate, shadow);
            foreach (var definition in CurrentStage.BridgeDefinitions)
            {
                var obj = new GameObject(definition.Name);
                obj.transform.SetParent(StageRoot, false);
                var bridge = obj.AddComponent<HAPLightBridge>();
                bridge.Initialize(definition, MakeMaterial(definition.Name + " surface", Teal, .1f), accent);
                bridges.Add(bridge);
            }
            yang = CreateActor("양", true, CurrentStage.SpawnYang, ivory, dark, accent);
            yin = CreateActor("음", false, CurrentStage.SpawnYin, dark, ivory, accent);
            BuildGoal(accent, ivory);
            BuildWayfinding(accent);
            SetupCameraAndLight();
            if (font == null) font = Resources.Load<Font>("HAPFont");
        }

        private HAPActor CreateActor(string label, bool isYang, Vector3 position, Material shell, Material contrast, Material accent)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(StageRoot, false);
            // Keep interpolation anchored to the authored spawn pose.
            obj.transform.SetPositionAndRotation(position, Quaternion.identity);
            var actor = obj.AddComponent<HAPActor>();
            actor.Initialize(isYang, shell, contrast, accent);
            actor.ResetActor(position);
            return actor;
        }

        private void BuildIsland(HAPStageDefinition.Solid definition, Material pale, Material warm, Material slate, Material shadow)
        {
            var root = new GameObject(definition.Name).transform;
            root.SetParent(StageRoot, false); root.position = definition.Center;
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.size = definition.Size;
            int nx = Mathf.Max(1, Mathf.RoundToInt(definition.Size.x));
            int nz = Mathf.Max(1, Mathf.RoundToInt(definition.Size.z));
            float dx = definition.Size.x / nx, dz = definition.Size.z / nz;
            for (int x = 0; x < nx; x++)
            for (int z = 0; z < nz; z++)
            {
                var center = new Vector3(-definition.Size.x * .5f + (x + .5f) * dx, 0,
                    -definition.Size.z * .5f + (z + .5f) * dz);
                VisualBox("Stone", root, center, new Vector3(dx - .014f, definition.Size.y, dz - .014f),
                    (x * 3 + z * 7) % 11 == 0 ? warm : pale);
                if (definition.Ceiling) continue;
                // Tapered underside: purely decorative, leaving every walking plane seamless.
                int depth = 1 + ((x + z * 3) % 3 == 0 ? 1 : 0);
                bool edge = x == 0 || x == nx - 1 || z == 0 || z == nz - 1;
                for (int d = 0; d < depth; d++)
                {
                    if (d > 0 && edge) continue;
                    VisualBox("Underside", root, center + Vector3.down * (definition.Size.y * .5f + .4f + d * .78f),
                        new Vector3(dx - .025f, .78f, dz - .025f), d == 0 ? slate : shadow);
                }
            }
            // Dark outer edge reads as one solid island under the small ceramic tiles.
            VisualBox("Structural edge", root, new Vector3(0, definition.Ceiling ? .13f : -.2f, 0),
                new Vector3(definition.Size.x + .035f, .15f, definition.Size.z + .035f), slate);
        }

        private void BuildGoal(Material accent, Material ivory)
        {
            var center = CurrentStage.GoalBounds.center;
            float y = CurrentStage.GoalGravitySign < 0 ? .025f : 5.975f;
            var root = new GameObject("두 사람의 목표").transform;
            root.SetParent(StageRoot, false); root.position = new Vector3(center.x, y, center.z);
            goalHalo = root;
            float extent = 1.34f;
            foreach (float sign in new[] { -1f, 1f })
            {
                VisualBox("Goal outline", root, new Vector3(sign * extent, 0, 0), new Vector3(.075f, .045f, 2.75f), accent);
                VisualBox("Goal outline", root, new Vector3(0, 0, sign * extent), new Vector3(2.75f, .045f, .075f), accent);
            }
            VisualBox("Two marks Yang", root, new Vector3(-.28f, 0, 0), new Vector3(.34f, .065f, .34f), ivory);
            VisualBox("Two marks Yin", root, new Vector3(.28f, 0, 0), new Vector3(.34f, .065f, .34f), accent);
            // Inverted goal uses little hanging markers, visible beneath its ceiling.
            if (CurrentStage.GoalGravitySign == 1)
            {
                foreach (float x in new[] { -1.34f, 1.34f })
                    VisualBox("Goal pendant", root, new Vector3(x, -.28f, -1.34f), new Vector3(.12f, .55f, .12f), accent);
            }
        }

        private void BuildWayfinding(Material accent)
        {
            if (StageIndex == 0) return;
            var root = new GameObject("중력 반전 위치").transform;
            root.SetParent(StageRoot, false);
            root.position = new Vector3(4, .025f, StageIndex == 1 ? 1.2f : 0);
            // Broken square marks the location aligned with a receiving ceiling.
            foreach (float x in new[] { -1.05f, 1.05f })
            foreach (float z in new[] { -1.05f, 1.05f })
                VisualBox("Flip corner", root, new Vector3(x, 0, z), new Vector3(.22f, .04f, .22f), accent);
            for (int i = 0; i < 3; i++)
                VisualBox("Flip stripe", root, new Vector3(0, 0, -.28f + i * .28f), new Vector3(.5f - i * .1f, .045f, .085f), accent);
        }

        public static GameObject VisualBox(string label, Transform parent, Vector3 localPosition, Vector3 size, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = label; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition; obj.transform.localScale = size;
            var collider = obj.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }

        private Material MakeMaterial(string name, Color color, float emission = 0)
        {
            var template = Resources.Load<Material>(emission > 0 ? "PrototypeEmissive" : "PrototypeSurface");
            Material material;
            if (template != null) material = new Material(template);
            else
            {
                var shader = Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("HAP surface shader is missing.");
                material = new Material(shader);
            }
            material.name = name; material.color = color;
            material.SetFloat("_Glossiness", .14f); material.SetFloat("_Metallic", 0f);
            if (emission > 0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }
            else material.DisableKeyword("_EMISSION");
            ownedMaterials.Add(material);
            return material;
        }

        private void SetupCameraAndLight()
        {
            var cameraObject = new GameObject("HAP diorama camera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(StageRoot, false);
            gameCamera = cameraObject.AddComponent<Camera>();
            gameCamera.orthographic = true;
            gameCamera.orthographicSize = CurrentStage.CameraSize;
            gameCamera.backgroundColor = new Color(.86f, .89f, .87f);
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
            gameCamera.nearClipPlane = .1f; gameCamera.farClipPlane = 150f;
            gameCamera.transform.position = CurrentStage.CameraPosition;
            gameCamera.transform.rotation = Quaternion.LookRotation(CurrentStage.CameraTarget - CurrentStage.CameraPosition, Vector3.up);
            gameCamera.allowHDR = true;
            var lightObject = new GameObject("Soft afternoon light");
            lightObject.transform.SetParent(StageRoot, false);
            lightObject.transform.rotation = Quaternion.Euler(48, -28, 0);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.1f;
            light.color = new Color(1f, .95f, .84f);
            light.shadows = LightShadows.Soft; light.shadowStrength = .55f; light.shadowBias = .04f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.73f, .79f, .79f);
            RenderSettings.ambientEquatorColor = new Color(.61f, .67f, .67f);
            RenderSettings.ambientGroundColor = new Color(.40f, .47f, .49f);
            RenderSettings.fog = false;
            QualitySettings.shadowDistance = 80f;
            gameCamera.rect = new Rect(0, 0, 1, 1);
        }

        private void ClearStage()
        {
            if (StageRoot != null)
            {
                StageRoot.gameObject.SetActive(false);
                Destroy(StageRoot.gameObject);
            }
            foreach (var material in ownedMaterials) if (material != null) Destroy(material);
            ownedMaterials.Clear(); bridges.Clear(); yang = null; yin = null; goalHalo = null; gameCamera = null;
            StageRoot = null;
        }

        private void OnDestroy()
        {
            ClearStage();
            if (roundedKeyMask != null) Destroy(roundedKeyMask);
        }

        private void EnsureControlsStyles()
        {
            if (stageLabelStyle != null) return;
            if (font == null) font = Resources.Load<Font>("HAPFont");
            stageLabelStyle = MakeControlsStyle(28, Ink, FontStyle.Normal);
            keyLabelStyle = MakeControlsStyle(16, Ink, FontStyle.Normal);
            keyLabelStyle.alignment = TextAnchor.MiddleCenter;
            roundedKeyMask = CreateRoundedKeyMask();
        }

        private static Texture2D CreateRoundedKeyMask()
        {
            const int size = 32;
            const float radius = 7f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HAP rounded key mask",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + .5f, py = y + .5f;
                float cx = Mathf.Clamp(px, radius, size - radius);
                float cy = Mathf.Clamp(py, radius, size - radius);
                float dx = px - cx, dy = py - cy;
                pixels[y * size + x] = dx * dx + dy * dy <= radius * radius ? Color.white : Color.clear;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private GUIStyle MakeControlsStyle(int size, Color color, FontStyle weight)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = weight };
            if (font != null) style.font = font;
            style.normal.textColor = color;
            return style;
        }

        private static void FillRect(Rect rect, Color color)
        {
            Color previous = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous;
        }

        private void DrawKeycap(Rect rect, string label, KeyCode key, int bit, float alpha, GUIStyle style)
        {
            bool pressed = Input.GetKey(key);
            bool seen = (tutorialKeyMask & bit) != 0;
            var previous = GUI.color;
            GUI.color = new Color(Ink.r, Ink.g, Ink.b, alpha * .78f);
            GUI.DrawTexture(rect, roundedKeyMask);
            GUI.color = pressed
                ? new Color(.28f, .77f, .71f, alpha)
                : new Color(KeyFill.r, KeyFill.g, KeyFill.b, alpha * KeyFill.a);
            GUI.DrawTexture(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), roundedKeyMask);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(rect, label, style);
            if (seen)
            {
                GUI.color = new Color(.16f, .62f, .54f, alpha);
                GUI.DrawTexture(new Rect(rect.xMax - 7, rect.y + 4, 3, 3), roundedKeyMask);
            }
            GUI.color = previous;
        }

        private void OnGUI()
        {
            if (CurrentStage == null) return;
            EnsureControlsStyles();
            GUI.color = Color.white;
            GUI.Label(new Rect(24, 18, 180, 42), "Stage" + (StageIndex + 1), stageLabelStyle);
            if (StageIndex == 0 && (tutorialKeyMask != AllTutorialKeys || tutorialFadeTime > 0f))
            {
                float alpha = tutorialKeyMask == AllTutorialKeys
                    ? Mathf.Clamp01(tutorialFadeTime / TutorialFadeDuration)
                    : 1f;
                float y = Screen.height - 105;
                DrawKeycap(new Rect(62, y, 40, 32), "W", KeyCode.W, 1 << 0, alpha, keyLabelStyle);
                DrawKeycap(new Rect(16, y + 36, 40, 32), "A", KeyCode.A, 1 << 1, alpha, keyLabelStyle);
                DrawKeycap(new Rect(62, y + 36, 40, 32), "S", KeyCode.S, 1 << 2, alpha, keyLabelStyle);
                DrawKeycap(new Rect(108, y + 36, 40, 32), "D", KeyCode.D, 1 << 3, alpha, keyLabelStyle);

                DrawKeycap(new Rect(166, y + 18, 54, 32), "Tab", KeyCode.Tab, 1 << 4, alpha, keyLabelStyle);
                DrawKeycap(new Rect(236, y + 18, 42, 32), "G", KeyCode.G, 1 << 5, alpha, keyLabelStyle);
                DrawKeycap(new Rect(294, y + 18, 42, 32), "R", KeyCode.R, 1 << 6, alpha, keyLabelStyle);
                DrawTabIcon(new Rect(166, y + 53, 54, 24), alpha);
                DrawVerticalArrows(new Rect(236, y + 53, 42, 24), alpha);
                DrawRotateIcon(new Rect(294, y + 53, 42, 24), alpha);
            }
        }

        private void DrawTabIcon(Rect rect, float alpha)
        {
            float blockSize = 11;
            float centerY = rect.y + 6;
            Rect left = new Rect(rect.x + 1, centerY, blockSize, blockSize);
            Rect right = new Rect(rect.xMax - blockSize - 1, centerY, blockSize, blockSize);
            DrawRoundedBlock(left, Ink, alpha);
            DrawRoundedBlock(right, Color.white, alpha);
            float cy = rect.center.y;
            DrawArrow(new Vector2(left.xMax + 2, cy - 3), new Vector2(right.x - 2, cy - 3), alpha);
            DrawArrow(new Vector2(right.x - 2, cy + 4), new Vector2(left.xMax + 2, cy + 4), alpha);
        }

        private void DrawVerticalArrows(Rect rect, float alpha)
        {
            Vector2 center = rect.center;
            DrawLine(new Vector2(center.x, rect.y + 5), new Vector2(center.x, rect.yMax - 5), alpha);
            DrawArrowHead(new Vector2(center.x, rect.y + 4), Vector2.down, alpha);
            DrawArrowHead(new Vector2(center.x, rect.yMax - 4), Vector2.up, alpha);
        }

        private void DrawRotateIcon(Rect rect, float alpha)
        {
            Vector2 center = rect.center;
            const float radius = 7f;
            const int segments = 9;
            float start = -48f * Mathf.Deg2Rad;
            float end = 222f * Mathf.Deg2Rad;
            Vector2 previous = center + new Vector2(Mathf.Cos(start), Mathf.Sin(start)) * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = Mathf.Lerp(start, end, i / (float)segments);
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                DrawLine(previous, point, alpha);
                previous = point;
            }
            float tangent = end + Mathf.PI * .5f;
            DrawArrowHead(previous, new Vector2(Mathf.Cos(tangent), Mathf.Sin(tangent)), alpha);
        }

        private void DrawRoundedBlock(Rect rect, Color color, float alpha)
        {
            GUI.color = new Color(color.r, color.g, color.b, alpha);
            GUI.DrawTexture(rect, roundedKeyMask);
            GUI.color = Color.white;
        }

        private void DrawArrow(Vector2 start, Vector2 end, float alpha)
        {
            DrawLine(start, end, alpha);
            DrawArrowHead(end, (end - start).normalized, alpha);
        }

        private void DrawArrowHead(Vector2 tip, Vector2 direction, float alpha)
        {
            Vector2 back = -direction.normalized * 4f;
            Vector2 side = new Vector2(-back.y, back.x) * .7f;
            DrawLine(tip, tip + back + side, alpha);
            DrawLine(tip, tip + back - side, alpha);
        }

        private void DrawLine(Vector2 start, Vector2 end, float alpha)
        {
            Vector2 delta = end - start;
            if (delta.sqrMagnitude < .01f) return;
            Matrix4x4 oldMatrix = GUI.matrix;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, start);
            FillRect(new Rect(start.x, start.y - .8f, delta.magnitude, 1.6f),
                new Color(Ink.r, Ink.g, Ink.b, alpha));
            GUI.matrix = oldMatrix;
        }
    }
}
