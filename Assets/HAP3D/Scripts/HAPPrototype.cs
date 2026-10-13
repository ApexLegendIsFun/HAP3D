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
        private float hintUntil;
        private string actionHint;
        private Font font;
        private GUIStyle titleStyle, headingStyle, bodyStyle, smallStyle, buttonStyle, whiteStyle;
        private static readonly Color Ink = new Color(.16f, .22f, .25f);
        private static readonly Color MutedInk = new Color(.39f, .45f, .46f);
        private static readonly Color Paper = new Color(.96f, .95f, .91f);
        private static readonly Color Teal = new Color(.20f, .55f, .51f);

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
        public bool IsTitleScreen { get; private set; }
        public IReadOnlyList<HAPLightBridge> Bridges => bridges.AsReadOnly();
        public HAPStageDefinition CurrentStage { get; private set; }
        public Transform StageRoot { get; private set; }

        private void Start()
        {
            if (CurrentStage != null) return;
            LoadStage(0);
            IsTitleScreen = true;
            yang.Body.isKinematic = true;
            yin.Body.isKinematic = true;
            SetCameraViewport();
        }

        private void Update()
        {
            if (automatedControl || CurrentStage == null) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (!Application.isEditor) Application.Quit();
                return;
            }
            if (IsTitleScreen)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) StartGame();
                return;
            }
            if (Input.GetKeyDown(KeyCode.R)) { RestartPrototype(); return; }
            if (won)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                {
                    if (StageIndex + 1 < StageCount) AdvanceStage();
                    else StartGame();
                }
                return;
            }
            if (Input.GetKeyDown(KeyCode.Tab)) SelectCharacter(1 - selectedIndex);
            if (Input.GetKeyDown(KeyCode.G)) TryFlipSelectedGravity();
            SetMoveInput(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")));
        }

        private void FixedUpdate()
        {
            if (yang == null || yin == null || IsTitleScreen) return;
            yang.RefreshGrounded(); yin.RefreshGrounded();
            if (won) return;
            yang.Simulate(selectedIndex == 0 ? moveInput : Vector2.zero, false);
            yin.Simulate(selectedIndex == 1 ? moveInput : Vector2.zero, false);
            foreach (var bridge in bridges) bridge.Tick(yang, yin, Time.fixedDeltaTime);
            if (!CurrentStage.PlayBounds.Contains(yang.Body.position) || !CurrentStage.PlayBounds.Contains(yin.Body.position))
            {
                RestartPrototype();
                ShowHint("괜찮아요. 다시, 함께.");
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
            yang.Animate(selectedIndex == 0 && !IsTitleScreen, IsTitleScreen || won, Time.deltaTime);
            yin.Animate(selectedIndex == 1 && !IsTitleScreen, IsTitleScreen || won, Time.deltaTime);
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
            if (IsTitleScreen || won || yang == null) return false;
            var actor = selectedIndex == 0 ? yang : yin;
            bool flipped = actor.TryFlip();
            ShowHint(flipped ? "발밑이 바뀌어도, 이동 방향은 같아요." : "발판에 서 있을 때 뒤집을 수 있어요.");
            return flipped;
        }

        public void StartGame() => LoadStage(0);

        public void LoadStage(int index)
        {
            if (index < 0 || index >= StageCount) throw new ArgumentOutOfRangeException(nameof(index));
            if (stages == null) stages = HAPStageDefinition.CreateStages();
            ClearStage();
            StageIndex = index; CurrentStage = stages[index]; IsTitleScreen = false;
            selectedIndex = 0; moveInput = Vector2.zero; won = false;
            visualClock = 0; hintUntil = 0; actionHint = null;
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

        public void RestartPrototype()
        {
            bool title = IsTitleScreen;
            LoadStage(StageIndex);
            if (!title) return;
            IsTitleScreen = true;
            yang.Body.isKinematic = true; yin.Body.isKinematic = true;
            SetCameraViewport();
        }

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
            // Interpolation must start at the spawn pose, including the frozen title preview.
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
            SetCameraViewport();
        }

        private void SetCameraViewport()
        {
            if (gameCamera == null) return;
            gameCamera.rect = IsTitleScreen ? new Rect(.25f, 0, .75f, 1) : new Rect(0, 0, 1, 1);
        }

        private void ShowHint(string message) { actionHint = message; hintUntil = Time.unscaledTime + 3.2f; }

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

        private void OnDestroy() => ClearStage();

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            if (font == null) font = Resources.Load<Font>("HAPFont");
            titleStyle = MakeStyle(72, Ink, FontStyle.Bold);
            headingStyle = MakeStyle(28, Ink, FontStyle.Bold);
            bodyStyle = MakeStyle(18, Ink);
            smallStyle = MakeStyle(14, MutedInk);
            whiteStyle = MakeStyle(17, Color.white, FontStyle.Bold);
            buttonStyle = MakeStyle(19, Color.white, FontStyle.Bold);
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            buttonStyle.normal.background = Texture2D.whiteTexture;
            buttonStyle.hover.background = Texture2D.whiteTexture;
            buttonStyle.active.background = Texture2D.whiteTexture;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;
        }

        private GUIStyle MakeStyle(int size, Color color, FontStyle weight = FontStyle.Normal)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = weight, wordWrap = true };
            if (font != null) style.font = font;
            style.normal.textColor = color;
            return style;
        }

        private static void Panel(Rect rect, Color color)
        {
            Color previous = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous;
        }

        private bool Button(Rect rect, string label)
        {
            Color previous = GUI.backgroundColor; GUI.backgroundColor = Teal;
            bool clicked = GUI.Button(rect, label, buttonStyle);
            GUI.backgroundColor = previous;
            return clicked;
        }

        private void OnGUI()
        {
            if (CurrentStage == null) return;
            EnsureStyles();
            var oldMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            scale = Mathf.Max(.35f, scale);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.color = Color.white;
            if (IsTitleScreen)
            {
                Panel(new Rect(0, 0, width * .30f, height), Paper);
                float y = height * .28f;
                GUI.Label(new Rect(52, y, 390, 100), "HAP 3D", titleStyle);
                GUI.Label(new Rect(58, y + 108, 350, 78), "둘이어서,\n이어지는 길.", headingStyle);
                GUI.Label(new Rect(58, y + 214, 335, 88), "양은 길을 밝히고, 음은 곁에 머물러요.\n작은 세상에서 두 사람의 길을 찾아보세요.", bodyStyle);
                if (Button(new Rect(58, y + 330, 290, 60), "시작하기    →")) StartGame();
                GUI.Label(new Rect(58, y + 404, 340, 35), "Enter 시작  ·  짧은 세 장면", smallStyle);
                GUI.Label(new Rect(58, height - 64, 390, 32), "이선동  /  음과 양의 작은 퍼즐", smallStyle);
            }
            else
            {
                Panel(new Rect(30, 26, 445, 113), new Color(Paper.r, Paper.g, Paper.b, .95f));
                GUI.Label(new Rect(50, 40, 380, 39), (StageIndex + 1).ToString("00") + "  " + CurrentStage.Name, headingStyle);
                GUI.Label(new Rect(51, 88, 380, 28), CurrentStage.Subtitle, smallStyle);
                for (int i = 0; i < StageCount; i++)
                    Panel(new Rect(width - 116 + i * 26, 43, 16, 5), i <= StageIndex ? Teal : new Color(.66f, .73f, .70f));
                Panel(new Rect(width - 250, 72, 214, 51), new Color(Ink.r, Ink.g, Ink.b, .94f));
                GUI.Label(new Rect(width - 229, 85, 195, 28), (selectedIndex == 0 ? "양  ·  " : "음  ·  ") + "선택됨", whiteStyle);
                float footerY = height - 116;
                Panel(new Rect(30, footerY, width - 60, 91), new Color(Paper.r, Paper.g, Paper.b, .96f));
                string hint = Time.unscaledTime < hintUntil ? actionHint : CurrentStage.Hint;
                foreach (var bridge in bridges)
                    if (bridge.State == HAPBridgeState.YinWarning) hint = "음이 혼자 있어요. 양을 곁으로 데려오세요.";
                GUI.Label(new Rect(50, footerY + 13, width - 110, 31), hint, bodyStyle);
                GUI.Label(new Rect(50, footerY + 51, width - 120, 26),
                    "WASD  이동       Tab  양 / 음 전환       G  중력 반전       R  다시 시작       Esc  종료", smallStyle);
                if (won) DrawComplete(width, height);
            }
            GUI.matrix = oldMatrix;
        }

        private void DrawComplete(float width, float height)
        {
            bool final = StageIndex == StageCount - 1;
            float x = width * .5f - 242, y = height * .5f - 150;
            Panel(new Rect(x, y, 484, 266), Paper);
            Panel(new Rect(x, y, 484, 5), Teal);
            GUI.Label(new Rect(x + 34, y + 30, 420, 44), final ? "끝까지, 함께." : "함께 도착했어요.", headingStyle);
            GUI.Label(new Rect(x + 35, y + 88, 414, 58), final ? "서로 다른 두 사람이\n하나의 길을 완성했습니다." : "다음 작은 세상에서도\n서로의 곁을 지켜주세요.", bodyStyle);
            if (Button(new Rect(x + 34, y + 172, 416, 58), final ? "처음부터 다시   ↻" : "다음 장면으로   →"))
            {
                if (final) StartGame(); else AdvanceStage();
            }
        }
    }
}
