#region

using System.Threading;
using UnityEngine;
using Wyd.Controllers.State;

#endregion

namespace Wyd.Graphics
{
    public class WireframeCube
    {
        private static readonly Color LineColor = new Color(50f, 50f, 50f, 1f);

        private readonly Material _LineMaterial;

        public Bounds Bounds { get; private set; }

        public WireframeCube(Vector3 center, Vector3 size, Material lineMaterial)
        {
            Bounds = new Bounds(center, size);
            _LineMaterial = lineMaterial;
        }

        public void RecalculateBounds(Vector3 center, Vector3 size)
        {
            Bounds = new Bounds(center, size);
        }

        public void Draw()
        {
            if (Thread.CurrentThread.ManagedThreadId == GameController.MainThreadId)
            {
                // don't try to run GL code on side thread
                return;
            }

            // draw bottom square
            DrawLine(Bounds.min, Bounds.min + Vector3.forward);
            DrawLine(Bounds.min, Bounds.min + Vector3.right);
            DrawLine(Bounds.min + Vector3.forward, Bounds.min + Vector3.forward + Vector3.right);
            DrawLine(Bounds.min + Vector3.right, Bounds.min + Vector3.forward + Vector3.right);

            // draw top square
            DrawLine(Bounds.min + Vector3.up, Bounds.min + Vector3.up + Vector3.forward);
            DrawLine(Bounds.min + Vector3.up, Bounds.min + Vector3.up + Vector3.right);
            DrawLine(Bounds.min + Vector3.up + Vector3.forward,
                Bounds.min + Vector3.up + Vector3.forward + Vector3.right);
            DrawLine(Bounds.min + Vector3.up + Vector3.right,
                Bounds.min + Vector3.up + Vector3.forward + Vector3.right);

            // draw vertical lines
            DrawLine(Bounds.min, Bounds.min + Vector3.up);
            DrawLine(Bounds.min + Vector3.forward, Bounds.min + Vector3.forward + Vector3.up);
            DrawLine(Bounds.min + Vector3.right, Bounds.min + Vector3.right + Vector3.up);
            DrawLine(Bounds.min + Vector3.forward + Vector3.right,
                Bounds.min + Vector3.forward + Vector3.right + Vector3.up);
        }

        private void DrawLine(Vector3 a, Vector3 b)
        {
            GL.Begin(GL.LINES);
            _LineMaterial.SetPass(0);
            GL.Color(LineColor);
            GL.Vertex3(a.x, a.y, a.z);
            GL.Vertex3(b.x, b.y, b.z);
            GL.End();
        }
    }
}
