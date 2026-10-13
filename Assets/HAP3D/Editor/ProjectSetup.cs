using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HAP3D.Editor
{
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/HAP3D/Scenes/Prototype.unity";
        private const string FontAssetPath = "Assets/HAP3D/Resources/HAPFont.ttf";
        private const string FontLicensePath = "Assets/HAP3D/Fonts/OFL-Nunito.txt";

        [MenuItem("HAP3D/Open Prototype")]
        public static void CreateSetupScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Directory.CreateDirectory("Assets/HAP3D/Scenes");
            Directory.CreateDirectory("Assets/HAP3D/Resources");
            AssetDatabase.Refresh();
            CreateSurfaceMaterial("PrototypeSurface", false);
            CreateSurfaceMaterial("PrototypeEmissive", true);
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("HAP 3D — Temporary Experiment").AddComponent<HAPPrototype>();
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Could not save prototype scene.");
            }
            else
                EditorSceneManager.OpenScene(ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            PlayerSettings.companyName = "Lee Seondong";
            PlayerSettings.productName = "HAP 3D";
            PlayerSettings.bundleVersion = "0.1.1";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
            ValidateSetup();
            Debug.Log("HAP3D_SETUP_OK");
        }

        private static void CreateSurfaceMaterial(string name, bool emissive)
        {
            string path = $"Assets/HAP3D/Resources/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("Built-in Standard shader missing.");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Glossiness", 0.25f);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.white);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
        }

        public static void ValidateSetup()
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException(ScenePath);
            if (AssetDatabase.LoadAssetAtPath<Font>(FontAssetPath) == null)
                throw new FileNotFoundException("Nunito ExtraBold font must be imported at " + FontAssetPath);
            if (!File.Exists(FontLicensePath)) throw new FileNotFoundException(FontLicensePath);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (scene.GetRootGameObjects().Count(g => g.GetComponent<HAPPrototype>() != null) != 1)
                throw new InvalidOperationException("Prototype requires exactly one HAPPrototype controller.");
        }

        [MenuItem("HAP3D/Build Mac Prototype")]
        public static void BuildMacSetup()
        {
            PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 2); // macOS Universal: Apple Silicon + Intel.
            BuildPrototype(BuildTarget.StandaloneOSX, "Builds/MacPrototype/HAP3D.app", "macOS");
        }

        [MenuItem("HAP3D/Build Windows 64-bit Prototype")]
        public static void BuildWindowsSetup()
        {
            BuildPrototype(BuildTarget.StandaloneWindows64, "Builds/WindowsPrototype/HAP3D.exe", "Windows 64-bit");
        }

        private static void BuildPrototype(BuildTarget target, string path, string platform)
        {
            ValidateSetup();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Build failed: {report.summary.result}, errors {report.summary.totalErrors}");

            var readmePath = Path.Combine(Path.GetDirectoryName(path), "README.txt");
            File.WriteAllText(readmePath, CreateBuildReadme(platform));
            WriteThirdPartyNotices(Path.GetDirectoryName(path));
            Debug.Log($"HAP3D_BUILD_OK {path}");
        }

        private static void WriteThirdPartyNotices(string outputDirectory)
        {
            var licenseText = File.ReadAllText(FontLicensePath);
            var notices =
                "Third-party font: Nunito ExtraBold (static weight 800 instance)\n" +
                "Copyright and license: SIL Open Font License 1.1\n" +
                "Source: https://github.com/google/fonts/tree/main/ofl/nunito\n\n" +
                "The upstream variable font was instantiated at weight 800 with fontTools.\n\n" +
                licenseText;
            File.WriteAllText(Path.Combine(outputDirectory, "THIRD_PARTY_NOTICES.txt"), notices);
        }

        private static string CreateBuildReadme(string platform)
        {
            return
                "HAP 3D — 3단계 빛블록 퍼즐 (v0.1.1) / 제작: 이선동 / 2026-10-13\n" +
                $"실행 환경: {platform}. 실행 파일과 같은 폴더의 파일을 함께 보관하고 전달하세요.\n" +
                "게임은 1단계부터 즉시 시작합니다. 화면 왼쪽 위에는 Stage1 / Stage2 / Stage3 중 현재 단계만 표시되고, 1단계에서만 조작 키캡을 볼 수 있습니다. 키캡은 누르면 반응하며 WASD·Tab·G·R을 모두 눌러 본 뒤 사라집니다. Esc는 종료키로 학습 대상에서 제외합니다.\n" +
                "각 단계 완료 뒤 0.9초 후 다음 단계로 자동 이동합니다. 마지막 목표에 도착한 뒤 R, Enter 또는 Space를 누르면 처음부터 다시 시작합니다.\n" +
                "WASD: 선택 캐릭터 이동. Tab: 양/음 캐릭터 전환. G: 접지 중 선택 캐릭터의 중력 반전.\n" +
                "R: 현재 단계 재시작. 마지막 목표에 도착한 뒤에는 처음부터 재시작. Esc: 게임 종료. 점프와 공중 이동은 없습니다.\n" +
                "빛블록: 양 단독이면 길 유지. 음 단독 접근이면 붕괴. 둘이 함께 있으면 안정된 길이 됩니다.\n" +
                "두 캐릭터는 각각 중력을 반전할 수 있습니다. 세 번째 퍼즐은 뒤집힌 빛다리를 건너는 구간입니다.\n" +
                "화면 글꼴: Nunito ExtraBold (정적 weight 800). SIL Open Font License 1.1. 자세한 내용은 THIRD_PARTY_NOTICES.txt를 참고하세요.\n" +
                "음 단독 붕괴 0.6초, 모두 영역을 떠난 뒤 복구 2초는 임시 실험 시간입니다. 판정 범위와 카메라도 실험 설정입니다.\n" +
                "키보드 조작은 PC 1인 플레이 기준입니다. 처음 플레이하는 사람 대상 테스트는 아직 하지 않았습니다.\n" +
                "Windows 빌드의 다른 PC에서 실행 확인은 아직 하지 않았습니다.\n";
        }
    }
}
