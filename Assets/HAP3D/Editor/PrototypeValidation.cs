using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using HAP3D;

namespace HAP3D.Editor
{
    /// <summary>Batch-friendly gameplay and stage lifecycle checks for the temporary prototype.</summary>
    [InitializeOnLoad]
    public static class PrototypeValidation
    {
        private const string ActiveKey = "HAP3D.PrototypeValidation.Active";
        private const float StageTimeout = 90f;
        private const float TotalTimeout = 360f;

        // Phases 2-6 preserve the original eight behavioral scenarios. The last phases add
        // title/stage lifecycle coverage and full input-driven routes through every authored stage.
        private static HAPPrototype prototype;
        private static int phase;
        private static int subStep;
        private static float phaseStart;
        private static float totalStart;
        private static float settledStart = -1f;
        private static bool phaseEntered;
        private static bool phaseSetupDone;
        private static bool segmentStarted;
        private static bool warningCaptured;
        private static bool titleSnapshotTaken;
        private static Vector3 titleYangStart;
        private static Vector3 titleYinStart;
        private static Transform retiredStageRoot;

        static PrototypeValidation()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        [MenuItem("HAP3D/Validate Prototype")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before starting HAP3D validation.");

            try
            {
                EditorSceneManager.OpenScene(ProjectSetup.ScenePath, OpenSceneMode.Single);
                prototype = null;
                phase = 0;
                subStep = 0;
                phaseEntered = false;
                warningCaptured = false;
                titleSnapshotTaken = false;
                activeMoveToken = -1;
                SessionState.SetBool(ActiveKey, true);
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception)
            {
                Fail("Could not start validation", exception);
            }
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (!EditorApplication.isPlaying)
            {
                if (prototype != null) Fail("Play Mode stopped before validation completed", null);
                return;
            }

            try
            {
                if (prototype == null)
                {
                    prototype = UnityEngine.Object.FindFirstObjectByType<HAPPrototype>();
                    if (prototype == null) return;
                    prototype.SetAutomatedControl(true);
                    totalStart = Time.time;
                    phase = 0;
                    subStep = 0;
                    phaseEntered = false;
                }

                if (Time.time - totalStart > TotalTimeout)
                    throw new TimeoutException($"Total validation exceeded {TotalTimeout:0}s.");
                if (!phaseEntered)
                {
                    phaseStart = Time.time;
                    settledStart = -1f;
                    phaseSetupDone = false;
                    segmentStarted = false;
                    phaseEntered = true;
                    Debug.Log($"HAP3D_VALIDATION_PHASE {phase + 1}/13");
                }
                if (Time.time - phaseStart > StageTimeout)
                    throw new TimeoutException($"Phase {phase + 1} exceeded {StageTimeout:0}s.");

                switch (phase)
                {
                    case 0: TitleScreenAndStart(); break;
                    case 1: StartupSettles(); break;
                    case 2: CollapsePersistsAndRestores(); break;
                    case 3: RestartDuringCollapse(); break;
                    case 4: PairStabilizesBridge(); break;
                    case 5: YangReachesCeilingAlone(); break;
                    case 6: FullStageZeroRoute(); break;
                    case 7: AdvanceToStage(1); break;
                    case 8: FullStageOneRoute(); break;
                    case 9: AdvanceToStage(2); break;
                    case 10: FullStageTwoRoute(); break;
                    case 11: HeightAwareBridgeStates(); break;
                    case 12: RestartAfterWin(); break;
                    default: Complete(); break;
                }
            }
            catch (Exception exception)
            {
                Fail($"Phase {phase + 1} failed", exception);
            }
        }

        private static void TitleScreenAndStart()
        {
            if (Elapsed < 0.35f) return;
            Check(prototype.IsTitleScreen, "Prototype must open on its title screen.");
            Check(prototype.StageIndex == 0 && prototype.StageCount == 3, "Title must point to stage 1 of 3.");
            Check(prototype.YangBody != null && prototype.YinBody != null, "Title screen actors must exist.");
            Check(Vector3.Distance(prototype.YangBody.position, prototype.CurrentStage.SpawnYang) < 0.02f &&
                  Vector3.Distance(prototype.YinBody.position, prototype.CurrentStage.SpawnYin) < 0.02f,
                "Title screen physics actors must appear at authored spawn positions.");
            Check(Vector3.Distance(prototype.YangBody.transform.position, prototype.CurrentStage.SpawnYang) < 0.02f &&
                  Vector3.Distance(prototype.YinBody.transform.position, prototype.CurrentStage.SpawnYin) < 0.02f,
                "Title screen actor transforms must appear at authored spawn positions before StartGame.");
            if (!titleSnapshotTaken)
            {
                titleYangStart = prototype.YangBody.position;
                titleYinStart = prototype.YinBody.position;
                titleSnapshotTaken = true;
                prototype.SelectCharacter(0);
                prototype.SetMoveInput(Vector2.right);
            }
            if (Elapsed < 0.8f) return;
            Check(Vector3.Distance(titleYangStart, prototype.YangBody.position) < 0.05f &&
                  Vector3.Distance(titleYinStart, prototype.YinBody.position) < 0.05f,
                "Actors must remain frozen before StartGame.");
            prototype.SetMoveInput(Vector2.zero);
            prototype.StartGame();
            Check(!prototype.IsTitleScreen && prototype.StageIndex == 0, "StartGame must enter stage 1.");
            Next();
        }

        private static void StartupSettles()
        {
            if (Elapsed < 0.75f) return;
            Check(prototype.YangBody != null && prototype.YinBody != null, "Both Rigidbody actors must exist.");
            Check(prototype.Bridges.Count == 1, "Stage 1 must contain one floor light bridge.");
            Check(prototype.YangGrounded && prototype.YinGrounded, "Both actors must settle on starting floor.");
            Check(prototype.Bridges[0].State.ToString() == "YangOnly", "Floor bridge must start in YangOnly state.");
            CaptureCamera("stage-1-start.png");
            Next();
        }

        // Original collapse / restore scenario. Teleports here isolate the state transition;
        // every complete stage route below uses movement input from its authored spawn.
        private static void CollapsePersistsAndRestores()
        {
            if (!phaseSetupDone)
            {
                prototype.RestartPrototype();
                Place(prototype.YangBody, new Vector3(-7f, 0.8f, -0.8f));
                Place(prototype.YinBody, new Vector3(-2f, 0.8f, 0.8f));
                phaseSetupDone = true;
                Physics.SyncTransforms();
            }

            if (!warningCaptured && Elapsed >= 0.18f)
            {
                Check(prototype.LightState == "YinWarning", $"Expected warning before collapse; got {prototype.LightState}.");
                CaptureCamera("stage-1-bridge-warning.png");
                warningCaptured = true;
            }

            if (subStep == 0 && Elapsed >= 0.8f)
            {
                Check(!prototype.LightCollider.enabled && prototype.LightState == "YinCollapsed",
                    $"Yin alone should collapse bridge; state={prototype.LightState}.");
                Check(prototype.YinBody.position.y < 0.45f,
                    $"Yin did not begin falling after collapse: y={prototype.YinBody.position.y:0.00}.");
                CaptureCamera("stage-1-bridge-collapsed.png");
                Place(prototype.YinBody, new Vector3(-5.5f, 0.8f, 0.8f));
                Physics.SyncTransforms();
                settledStart = Time.time;
                subStep = 1;
            }
            else if (subStep == 1 && Time.time - settledStart >= 0.25f)
            {
                Place(prototype.YinBody, new Vector3(-2f, 0.8f, 0.8f));
                Physics.SyncTransforms();
                settledStart = Time.time;
                subStep = 2;
            }
            else if (subStep == 2 && Time.time - settledStart >= 0.2f)
            {
                Check(!prototype.LightCollider.enabled && prototype.LightState == "YinCollapsed",
                    "Collapsed bridge must remain collapsed when Yin re-enters alone.");
                Place(prototype.YinBody, new Vector3(-5.5f, 0.8f, 0.8f));
                Physics.SyncTransforms();
                settledStart = Time.time;
                subStep = 3;
            }
            else if (subStep == 3 && Time.time - settledStart >= 2.1f)
            {
                Check(prototype.LightCollider.enabled && prototype.LightState == "YangOnly",
                    $"Bridge should restore after both actors leave for 2s; state={prototype.LightState}.");
                Next();
            }
        }

        private static void RestartDuringCollapse()
        {
            if (!phaseSetupDone)
            {
                prototype.RestartPrototype();
                Place(prototype.YangBody, new Vector3(-7f, 0.8f, -0.8f));
                Place(prototype.YinBody, new Vector3(-2f, 0.8f, 0.8f));
                Physics.SyncTransforms();
                phaseSetupDone = true;
            }
            if (Elapsed < 0.72f) return;
            Check(!prototype.LightCollider.enabled && prototype.LightState == "YinCollapsed", "Test setup did not reach collapse.");
            prototype.RestartPrototype();
            Check(prototype.LightCollider.enabled, "Restart must restore light bridge.");
            Check(prototype.YangGravitySign == -1 && prototype.YinGravitySign == -1, "Restart must restore baseline gravity.");
            Check(prototype.YangBody.position == prototype.CurrentStage.SpawnYang, "Restart must return Yang to stage spawn.");
            Check(prototype.YinBody.position == prototype.CurrentStage.SpawnYin, "Restart must return Yin to stage spawn.");
            Check(prototype.YangBody.linearVelocity.sqrMagnitude < 0.001f && prototype.YinBody.linearVelocity.sqrMagnitude < 0.001f,
                "Restart must clear actor velocities.");
            Next();
        }

        private static void PairStabilizesBridge()
        {
            if (!phaseSetupDone)
            {
                prototype.RestartPrototype();
                Place(prototype.YangBody, prototype.CurrentStage.SpawnYang);
                Place(prototype.YinBody, new Vector3(-2f, 0.8f, 0.8f));
                phaseSetupDone = true;
                Physics.SyncTransforms();
                settledStart = Time.time;
            }
            if (subStep == 0 && Time.time - settledStart >= 0.2f)
            {
                Check(prototype.LightState == "YinWarning", "Yin alone must enter warning state before Yang joins.");
                prototype.SelectCharacter(0);
                Place(prototype.YangBody, new Vector3(-2f, 0.8f, -0.8f));
                Physics.SyncTransforms();
                settledStart = Time.time;
                subStep = 1;
            }
            if (subStep != 1 || Time.time - settledStart < 0.5f) return;
            Check(prototype.LightCollider.enabled && prototype.LightState == "Stable", "Together state must stabilize bridge.");
            Check(prototype.YangBody.position.y > 0.55f && prototype.YinBody.position.y > 0.55f,
                "Both actors must remain supported after stabilization.");
            Next();
        }

        private static void YangReachesCeilingAlone()
        {
            if (!phaseSetupDone)
            {
                prototype.LoadStage(1);
                Place(prototype.YangBody, new Vector3(4f, 0.8f, 0.8f));
                Physics.SyncTransforms();
                phaseSetupDone = true;
            }
            if (Elapsed < 0.45f) return;
            if (subStep == 0)
            {
                prototype.SelectCharacter(0);
                Check(prototype.YangGrounded, "Yang must be grounded before gravity flip.");
                Check(prototype.TryFlipSelectedGravity(), "Grounded Yang gravity flip should succeed.");
                subStep = 1;
            }
            if (Elapsed < 2.8f) return;
            Check(prototype.YangGrounded && prototype.YangBody.position.y > 4.8f && prototype.YangBody.position.y < 5.8f,
                $"Yang should settle on ceiling: grounded={prototype.YangGrounded}, y={prototype.YangBody.position.y:0.00}.");
            Check(!prototype.GoalReached, "One character alone must not win.");
            CaptureCamera("stage-1-ceiling-single.png");
            Next();
        }

        // Real input route across bridge and landing from authored stage spawn.
        private static void FullStageZeroRoute()
        {
            if (!phaseSetupDone)
            {
                prototype.LoadStage(0);
                prototype.AdvanceStage();
                Check(prototype.StageIndex == 0, "AdvanceStage must reject an incomplete stage.");
                phaseSetupDone = true;
            }
            if (Elapsed < 0.75f) return;
            if (subStep < 6)
            {
                var routeStep = subStep;
                MoveActorToward(routeStep % 2, new Vector2(1f, 0f), new Vector3(new[] { 0f, 0f, 2.6f, 2.6f, 4f, 4f }[routeStep], 0f, 0f), routeStep);
                if (prototype.GoalReached)
                {
                    Check(prototype.CurrentStage.GoalBounds.Contains(prototype.YangBody.position) &&
                          prototype.CurrentStage.GoalBounds.Contains(prototype.YinBody.position),
                        "Both actors must enter goal bounds to complete full Stage 1 route.");
                    CaptureCamera("stage-1-goal.png");
                    Next();
                    return;
                }
                if (subStep > routeStep && (routeStep == 1 || routeStep == 2 || routeStep == 3))
                    Check(prototype.Bridges[0].State.ToString() == "Stable", "Both actors crossing bridge must stabilize it.");
                return;
            }
            throw new InvalidOperationException("Both actors reached landing without completing Stage 1 goal.");
        }

        private static void FullStageOneRoute()
        {
            if (!phaseSetupDone)
            {
                Check(prototype.StageIndex == 1 && prototype.Bridges.Count == 0, "Stage 2 must load without light bridges.");
                Check(Vector3.Distance(prototype.YangBody.position, prototype.CurrentStage.SpawnYang) < 0.1f &&
                      Vector3.Distance(prototype.YinBody.position, prototype.CurrentStage.SpawnYin) < 0.1f, "Stage 2 must reset both actors to authored spawns.");
                CaptureCamera("stage-2-start.png");
                phaseSetupDone = true;
            }
            if (subStep < 2)
            {
                MoveActorToward(subStep, Vector2.right, new Vector3(4f, 0f, 0f), subStep);
                return;
            }
            if (subStep == 2)
            {
                prototype.SelectCharacter(0);
                Check(prototype.TryFlipSelectedGravity(), "Stage 2 Yang must flip from floor.");
                subStep = 3;
            }
            else if (subStep == 3 && prototype.YangGrounded)
            {
                CaptureCamera("stage-2-ceiling.png");
                prototype.SelectCharacter(1);
                Check(prototype.TryFlipSelectedGravity(), "Stage 2 Yin must flip from floor.");
                subStep = 4;
            }
            else if (subStep == 4 && prototype.YinGrounded)
            {
                subStep = 5;
                segmentStarted = false;
            }
            else if (subStep == 5)
            {
                MoveActorToward(0, Vector2.down, new Vector3(4f, 0f, -3f), 5);
                if (subStep == 6) segmentStarted = false;
            }
            else if (subStep == 6)
            {
                MoveActorToward(1, Vector2.down, new Vector3(4f, 0f, -3f), 6);
                if (subStep == 7)
                {
                    Check(prototype.GoalReached, "Both inverted actors must reach Stage 2 goal.");
                    CaptureCamera("stage-2-goal.png");
                    Next();
                }
            }
        }

        private static void FullStageTwoRoute()
        {
            if (!phaseSetupDone)
            {
                Check(prototype.StageIndex == 2 && prototype.Bridges.Count == 2, "Stage 3 must have independent floor and ceiling bridges.");
                Check(Vector3.Distance(prototype.YangBody.position, prototype.CurrentStage.SpawnYang) < 0.1f &&
                      Vector3.Distance(prototype.YinBody.position, prototype.CurrentStage.SpawnYin) < 0.1f, "Stage 3 must reset both actors to authored spawns.");
                CaptureCamera("stage-3-start.png");
                phaseSetupDone = true;
            }
            if (subStep < 6)
            {
                MoveActorToward(subStep % 2, Vector2.right,
                    new Vector3(new[] { 0f, 0f, 2.6f, 2.6f, 4f, 4f }[subStep], 0f, 0f), subStep);
                return;
            }
            if (subStep == 6)
            {
                Check(prototype.YangBody.position.x > 3.7f && prototype.YinBody.position.x > 3.7f,
                    "Both actors must cross Stage 3 floor bridge through movement input.");
                prototype.SelectCharacter(0);
                Check(prototype.TryFlipSelectedGravity(), "Stage 3 Yang must flip from floor.");
                subStep = 7;
            }
            else if (subStep == 7 && prototype.YangGrounded)
            {
                prototype.SelectCharacter(1);
                Check(prototype.TryFlipSelectedGravity(), "Stage 3 Yin must flip from floor.");
                subStep = 8;
            }
            else if (subStep == 8 && prototype.YinGrounded)
            {
                subStep = 9;
                segmentStarted = false;
            }
            else if (subStep >= 9 && subStep <= 14)
            {
                var routeStep = subStep;
                var actor = (subStep - 9) % 2;
                var z = new[] { -4f, -4f, -6.4f, -6.4f, -8f, -8f }[subStep - 9];
                MoveActorToward(actor, Vector2.down, new Vector3(4f, 0f, z), 20 + subStep);
                if (routeStep == 10 && subStep > routeStep)
                    Check(BridgeAtCeiling().State.ToString() == "Stable", "Same-height pair must stabilize Stage 3 ceiling bridge.");
                if (subStep == 15)
                {
                    Check(prototype.GoalReached, "Both inverted actors must cross ceiling bridge and reach Stage 3 goal.");
                    CaptureCamera("stage-3-goal.png");
                    Next();
                }
            }
        }

        private static void HeightAwareBridgeStates()
        {
            if (!phaseSetupDone)
            {
                var floorBridge = BridgeAtFloor();
                var ceilingBridge = BridgeAtCeiling();
                stageTwoFloorBridge = floorBridge;
                stageTwoCeilingBridge = ceilingBridge;
                stageTwoFloorBridge.Tick(new Vector3(-1f, 0.8f, -0.8f), new Vector3(-1f, 0.8f, 0.8f), 0.02f);
                Check(stageTwoFloorBridge.State.ToString() == "Stable", "Stage 3 floor bridge must stabilize for a same-height pair.");
                stageTwoCeilingBridge.Tick(new Vector3(-1f, 0.8f, -0.8f), new Vector3(-1f, 0.8f, 0.8f), 0.02f);
                Check(stageTwoCeilingBridge.State.ToString() != "Stable", "Floor actors must not stabilize ceiling bridge.");
                stageTwoFloorBridge.Tick(new Vector3(-1f, 0.8f, -0.8f), new Vector3(-1f, 5.2f, 0.8f), 0.02f);
                Check(stageTwoFloorBridge.State.ToString() != "Stable", "Actors at different heights must not stabilize a bridge.");
                stageTwoCeilingBridge.Tick(new Vector3(4f, 5.2f, -4f), new Vector3(4f, 5.2f, -4f), 0.02f);
                Check(stageTwoCeilingBridge.State.ToString() == "Stable", "Stage 3 ceiling bridge must stabilize independently.");
                stageTwoFloorBridge.Tick(new Vector3(4f, 5.2f, -4f), new Vector3(4f, 5.2f, -4f), 0.02f);
                Check(stageTwoFloorBridge.State.ToString() != "Stable", "Ceiling actors must not stabilize floor bridge.");
                phaseSetupDone = true;
                Next();
                return;
            }
        }

        private static HAPLightBridge stageTwoFloorBridge;
        private static HAPLightBridge stageTwoCeilingBridge;

        private static HAPLightBridge BridgeAtFloor() => BridgeAtHeight(false);
        private static HAPLightBridge BridgeAtCeiling() => BridgeAtHeight(true);

        private static HAPLightBridge BridgeAtHeight(bool ceiling)
        {
            foreach (var bridge in prototype.Bridges)
                if ((bridge.Collider.bounds.center.y > 3f) == ceiling) return bridge;
            throw new InvalidOperationException($"Missing {(ceiling ? "ceiling" : "floor")} bridge.");
        }

        private static void RestartAfterWin()
        {
            Check(prototype.GoalReached, "Previous stage must end in a win.");
            prototype.RestartPrototype();
            Check(!prototype.GoalReached, "Restart after win must clear goal state.");
            Check(prototype.YangGravitySign == -1 && prototype.YinGravitySign == -1,
                "Restart after win must restore baseline gravity.");
            Check(prototype.YangBody.position == prototype.CurrentStage.SpawnYang &&
                  prototype.YinBody.position == prototype.CurrentStage.SpawnYin,
                "Restart after win must reset both actors to current stage spawns.");
            Complete();
        }

        private static void AdvanceToStage(int targetIndex)
        {
            if (!phaseSetupDone)
            {
                Check(prototype.GoalReached, "Only a completed stage may advance.");
                Check(prototype.StageIndex == targetIndex - 1, "Advance started from wrong stage index.");
                retiredStageRoot = prototype.StageRoot;
                var previousBridgeCount = prototype.Bridges.Count;
                prototype.AdvanceStage();
                Check(prototype.StageIndex == targetIndex, "AdvanceStage must select next stage.");
                Check(!prototype.IsTitleScreen && !prototype.GoalReached, "Next stage must start active and reset goal.");
                Check(prototype.YangGravitySign == -1 && prototype.YinGravitySign == -1,
                    "Stage transition must reset gravity to authored baseline.");
                Check(prototype.YangBody.position == prototype.CurrentStage.SpawnYang &&
                      prototype.YinBody.position == prototype.CurrentStage.SpawnYin,
                    "Stage transition must reset actors to authored spawns.");
                Check(prototype.StageRoot != retiredStageRoot, "Stage transition must replace stage root.");
                if (targetIndex == 1) Check(previousBridgeCount == 1 && prototype.Bridges.Count == 0,
                    "Stage transition must clean up previous bridge objects.");
                if (targetIndex == 2) Check(previousBridgeCount == 0 && prototype.Bridges.Count == 2,
                    "Stage transition must create only Stage 3 bridge objects.");
                phaseSetupDone = true;
                settledStart = Time.time;
            }
            if (Time.time - settledStart < 0.15f) return;
            Check(retiredStageRoot == null && prototype.transform.childCount == 1,
                "Stage transition must destroy prior stage hierarchy before continuing.");
            Next();
        }

        private static void MoveActorToward(int actorIndex, Vector2 direction, Vector3 target, int moveToken)
        {
            var body = actorIndex == 0 ? prototype.YangBody : prototype.YinBody;
            if (!segmentStarted || moveToken != activeMoveToken)
            {
                prototype.SelectCharacter(actorIndex);
                activeMoveToken = moveToken;
                segmentStarted = true;
                settledStart = -1f;
            }

            if (prototype.GoalReached)
            {
                Check(prototype.CurrentStage.GoalBounds.Contains(prototype.YangBody.position) &&
                      prototype.CurrentStage.GoalBounds.Contains(prototype.YinBody.position),
                    "Goal completion requires both actors inside goal bounds.");
                subStep++;
                segmentStarted = false;
                return;
            }

            var distance = direction.x != 0f ? target.x - body.position.x : target.z - body.position.z;
            var directionComponent = direction.x != 0f ? direction.x : direction.y;
            if (distance * directionComponent > 0.08f)
            {
                prototype.SetMoveInput(direction);
                return;
            }

            prototype.SetMoveInput(Vector2.zero);
            if (settledStart < 0f) settledStart = Time.time;
            if (Time.time - settledStart < 0.1f) return;
            Check(body.position.y > 4.8f && (actorIndex == 0 ? prototype.YangGrounded : prototype.YinGrounded)
                    || body.position.y < 1.4f && (actorIndex == 0 ? prototype.YangGrounded : prototype.YinGrounded),
                $"{(actorIndex == 0 ? "Yang" : "Yin")} lost support while moving to {target}.");
            subStep++;
            segmentStarted = false;
            settledStart = -1f;
        }

        private static int activeMoveToken = -1;
        private static float Elapsed => Time.time - phaseStart;

        private static void Place(Rigidbody body, Vector3 position)
        {
            body.position = position;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private static void CaptureCamera(string filename)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.Log($"HAP3D_VALIDATION_CAPTURE_SKIPPED no graphics device: {filename}");
                return;
            }
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("Main camera missing during validation capture.");

            const int width = 1280;
            const int height = 720;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            Texture2D image = null;
            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply();
                var directory = Path.Combine("Builds", "Validation");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, filename), image.EncodeToPNG());
                Debug.Log($"HAP3D_VALIDATION_CAPTURE {Path.Combine(directory, filename)}");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (image != null) UnityEngine.Object.Destroy(image);
                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Next()
        {
            prototype.SetMoveInput(Vector2.zero);
            phase++;
            subStep = 0;
            phaseEntered = false;
            phaseSetupDone = false;
            segmentStarted = false;
            activeMoveToken = -1;
        }

        private static void Complete()
        {
            SessionState.SetBool(ActiveKey, false);
            Debug.Log("HAP3D_VALIDATION_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
            else EditorApplication.isPlaying = false;
        }

        private static void Fail(string message, Exception exception)
        {
            SessionState.SetBool(ActiveKey, false);
            if (exception == null) Debug.LogError("HAP3D_VALIDATION_FAILED: " + message);
            else Debug.LogError("HAP3D_VALIDATION_FAILED: " + message + "\n" + exception);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else EditorApplication.isPlaying = false;
        }
    }
}
