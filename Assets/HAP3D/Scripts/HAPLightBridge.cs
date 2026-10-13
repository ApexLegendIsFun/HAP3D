using System.Collections.Generic;
using UnityEngine;

namespace HAP3D
{
    public enum HAPBridgeState { YangOnly, YinWarning, YinCollapsed, Stable }

    /// <summary>Many visible voxels, one continuous supporting collider and one local rule.</summary>
    public sealed class HAPLightBridge : MonoBehaviour
    {
        public const float CollapseDelay = .6f;
        public const float RestoreDelay = 2f;
        public BoxCollider Collider { get; private set; }
        public HAPBridgeState State { get; private set; } = HAPBridgeState.YangOnly;
        public Vector3 Center => transform.position;
        public Vector3 Size { get; private set; }
        public int GravitySign { get; private set; }
        public Bounds LocalApproachBounds { get; private set; }
        public Bounds ApproachBounds => new Bounds(transform.TransformPoint(LocalApproachBounds.center), LocalApproachBounds.size);
        public Bounds WorldBounds => new Bounds(Center, Size);
        public float YinOnlyTime => yinOnlyTime;
        public float EmptyTime => emptyTime;
        private readonly List<Transform> cells = new List<Transform>();
        private readonly List<Vector3> cellPositions = new List<Vector3>();
        private readonly List<Renderer> cellRenderers = new List<Renderer>();
        private Material material;
        private Vector3 cellScale;
        private float yinOnlyTime;
        private float emptyTime;
        private float pulse;
        private bool collapsed;

        public void Initialize(HAPStageDefinition.Bridge definition, Material surface, Material edge)
        {
            transform.position = definition.Center;
            Size = definition.Size;
            GravitySign = definition.GravitySign;
            LocalApproachBounds = new Bounds(definition.ApproachBounds.center - Center, definition.ApproachBounds.size);
            material = surface;
            Collider = gameObject.AddComponent<BoxCollider>();
            Collider.size = Size;
            int countX = Mathf.Max(1, Mathf.RoundToInt(Size.x));
            int countZ = Mathf.Max(1, Mathf.RoundToInt(Size.z));
            cellScale = new Vector3(Size.x / countX - .035f, Size.y * .8f, Size.z / countZ - .035f);
            for (int x = 0; x < countX; x++)
            for (int z = 0; z < countZ; z++)
            {
                var position = new Vector3(-Size.x * .5f + (x + .5f) * Size.x / countX, 0,
                    -Size.z * .5f + (z + .5f) * Size.z / countZ);
                var cell = HAPPrototype.VisualBox("Light voxel", transform, position, cellScale, material);
                cells.Add(cell.transform); cellPositions.Add(position); cellRenderers.Add(cell.GetComponent<Renderer>());
            }
            // Sparse edge guides keep a collapsed crossing legible; they never support actors.
            if (Size.x >= Size.z)
            {
                foreach (float x in new[] { -Size.x * .5f, Size.x * .5f })
                foreach (float z in new[] { -Size.z * .5f, Size.z * .5f })
                    HAPPrototype.VisualBox("Bridge edge", transform, new Vector3(x, 0, z), new Vector3(.09f, .09f, .28f), edge);
            }
            else
            {
                foreach (float x in new[] { -Size.x * .5f, Size.x * .5f })
                foreach (float z in new[] { -Size.z * .5f, Size.z * .5f })
                    HAPPrototype.VisualBox("Bridge edge", transform, new Vector3(x, 0, z), new Vector3(.28f, .09f, .09f), edge);
            }
            ResetState();
        }

        public bool ContainsApproach(Vector3 worldPosition) => LocalApproachBounds.Contains(transform.InverseTransformPoint(worldPosition));
        public void Tick(Rigidbody yang, Rigidbody yin, float dt) => Tick(yang.position, yin.position, dt);
        public void Tick(HAPActor yang, HAPActor yin, float dt) => Tick(yang.Body.position, yin.Body.position, dt);

        public void Tick(Vector3 yang, Vector3 yin, float dt)
        {
            bool yangPresent = ContainsApproach(yang);
            bool yinPresent = ContainsApproach(yin);
            if (yangPresent && yinPresent)
            {
                State = HAPBridgeState.Stable;
                collapsed = false; yinOnlyTime = 0; emptyTime = 0;
            }
            else if (yinPresent)
            {
                emptyTime = 0;
                if (!collapsed) yinOnlyTime += dt;
                if (yinOnlyTime >= CollapseDelay) collapsed = true;
                State = collapsed ? HAPBridgeState.YinCollapsed : HAPBridgeState.YinWarning;
            }
            else if (yangPresent)
            {
                emptyTime = 0;
                if (!collapsed) { State = HAPBridgeState.YangOnly; yinOnlyTime = 0; }
            }
            else
            {
                yinOnlyTime = 0;
                if (collapsed)
                {
                    emptyTime += dt;
                    if (emptyTime >= RestoreDelay) { collapsed = false; State = HAPBridgeState.YangOnly; emptyTime = 0; }
                }
                else { State = HAPBridgeState.YangOnly; emptyTime = 0; }
            }
            Collider.enabled = !collapsed;
            pulse += dt;
            RefreshVisual();
        }

        public void ResetState()
        {
            State = HAPBridgeState.YangOnly;
            collapsed = false; yinOnlyTime = 0; emptyTime = 0; pulse = 0;
            if (Collider != null) Collider.enabled = true;
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (material == null) return;
            bool warning = State == HAPBridgeState.YinWarning;
            var color = warning ? new Color(.94f, .48f, .20f) :
                State == HAPBridgeState.Stable ? new Color(.29f, .81f, .68f) : new Color(.32f, .68f, .67f);
            material.color = color;
            material.SetColor("_EmissionColor", color * (warning ? .18f : State == HAPBridgeState.Stable ? .25f : .08f));
            for (int i = 0; i < cells.Count; i++)
            {
                cellRenderers[i].enabled = !collapsed;
                var position = cellPositions[i];
                if (warning) position.y += Mathf.Sin(pulse * 32f + i * 1.7f) * .055f;
                cells[i].localPosition = position;
                cells[i].localScale = cellScale * (warning ? .92f : 1f);
            }
        }
    }
}
