using System;
using System.Collections.Generic;
using UnityEngine;

namespace HAP3D
{
    /// <summary>Authored geometry. Controls, approach margins and timings remain experimental.</summary>
    public sealed class HAPStageDefinition
    {
        public sealed class Solid
        {
            public string Name { get; }
            public Vector3 Center { get; }
            public Vector3 Size { get; }
            public bool Ceiling { get; }
            public Solid(string name, Vector3 center, Vector3 size, bool ceiling = false)
            { Name = name; Center = center; Size = size; Ceiling = ceiling; }
        }

        public sealed class Bridge
        {
            public string Name { get; }
            public Vector3 Center { get; }
            public Vector3 Size { get; }
            public int GravitySign { get; }
            public Bounds ApproachBounds { get; }
            public Bridge(string name, Vector3 center, Vector3 size, int gravitySign)
            {
                Name = name; Center = center; Size = size; GravitySign = gravitySign;
                // The vertical interval follows the walking side, never the other floor.
                var surfaceY = center.y - gravitySign * size.y * .5f;
                ApproachBounds = new Bounds(new Vector3(center.x, surfaceY - gravitySign * .7f, center.z),
                    new Vector3(size.x + 1.6f, 3.2f, size.z + 1.6f));
            }
        }

        public string Name { get; }
        public string Subtitle { get; }
        public string Hint { get; }
        public Vector3 SpawnYang { get; }
        public Vector3 SpawnYin { get; }
        public Bounds GoalBounds { get; }
        public int GoalGravitySign { get; }
        public Bounds PlayBounds { get; }
        public Vector3 CameraPosition { get; }
        public Vector3 CameraTarget { get; }
        public float CameraSize { get; }
        public IReadOnlyList<Solid> Solids { get; }
        public IReadOnlyList<Bridge> BridgeDefinitions { get; }

        private HAPStageDefinition(string name, string subtitle, string hint, Vector3 yang, Vector3 yin,
            Vector3 goal, int goalGravity, Solid[] solids, Bridge[] bridges,
            Vector3 camera, Vector3 cameraTarget, float cameraSize)
        {
            Name = name; Subtitle = subtitle; Hint = hint; SpawnYang = yang; SpawnYin = yin;
            GoalGravitySign = goalGravity;
            GoalBounds = new Bounds(goal, new Vector3(2.8f, 1.5f, 2.8f));
            PlayBounds = new Bounds(new Vector3(0f, 3f, -3f), new Vector3(24f, 20f, 24f));
            Solids = Array.AsReadOnly(solids); BridgeDefinitions = Array.AsReadOnly(bridges);
            CameraPosition = camera; CameraTarget = cameraTarget; CameraSize = cameraSize;
        }

        public static HAPStageDefinition[] CreateStages()
        {
            var lowerStart = new Solid("출발 섬", new Vector3(-6, -.5f, 0), new Vector3(4, 1, 4));
            var lowerLanding = new Solid("도착 섬", new Vector3(4, -.5f, 0), new Vector3(4, 1, 4));
            var lowerBridge = new Bridge("첫 번째 빛다리", new Vector3(-1, -.18f, 0), new Vector3(6, .36f, 4), -1);
            return new[]
            {
                new HAPStageDefinition("함께 건너기", "하나의 길, 두 개의 발걸음",
                    "양이 먼저, 음은 곁으로. 서로 멀어지지 않게 건너세요.",
                    new Vector3(-7, .8f, -.8f), new Vector3(-5.5f, .8f, .8f), new Vector3(4, .8f, 0), -1,
                    new[] { lowerStart, lowerLanding }, new[] { lowerBridge },
                    new Vector3(17, 14, -22), new Vector3(-1, 0, 0), 7.3f),
                new HAPStageDefinition("다른 바닥", "위라고 생각했던 곳에 서다",
                    "받아 줄 발판 아래에서 G. 뒤집힌 바닥을 따라 목표로 가세요.",
                    new Vector3(-7, .8f, .8f), new Vector3(-5.5f, .8f, 2), new Vector3(4, 5.2f, -3), 1,
                    new[]
                    {
                        new Solid("아래 산책길", new Vector3(-1, -.5f, 1.6f), new Vector3(14, 1, 4)),
                        new Solid("위 산책길", new Vector3(4, 6.25f, -1.2f), new Vector3(4, .5f, 7.2f), true)
                    }, Array.Empty<Bridge>(),
                    new Vector3(22, 6, -28), new Vector3(-.5f, 2.5f, -.6f), 8.6f),
                new HAPStageDefinition("뒤집혀도 함께", "같은 마음으로, 다른 하늘로",
                    "다리를 건너 뒤집고, 다시 함께. 두 사람 모두 마지막 빛으로.",
                    new Vector3(-7, .8f, -.8f), new Vector3(-5.5f, .8f, .8f), new Vector3(4, 5.2f, -8), 1,
                    new[]
                    {
                        lowerStart, lowerLanding,
                        new Solid("반전 쉼터", new Vector3(4, 6.25f, 0), new Vector3(4, .5f, 4), true),
                        new Solid("마지막 섬", new Vector3(4, 6.5f, -8), new Vector3(4, 1, 4), true)
                    },
                    new[] { lowerBridge, new Bridge("천장의 빛다리", new Vector3(4, 6.18f, -4), new Vector3(4, .36f, 4), 1) },
                    new Vector3(24, 6, -30), new Vector3(-.5f, 2.5f, -3), 10.1f)
            };
        }
    }
}
