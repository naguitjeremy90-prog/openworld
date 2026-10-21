using UnityEngine;
using UnityEngine.UI;

namespace Alaala.Authentication
{
    // Small uGUI vector icon: no textures, sprite imports, or generated art assets.
    public sealed class PasswordVisibilityEyeGraphic : MaskableGraphic
    {
        [SerializeField] private bool passwordVisible;

        public void SetPasswordVisible(bool visible)
        {
            if (passwordVisible == visible) return;
            passwordVisible = visible;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float scale = Mathf.Min(rect.width / 28f, rect.height / 24f);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 16; i++)
                {
                    float a = i / 16f, b = (i + 1) / 16f;
                    Line(mesh, center + scale * new Vector2(-12 + 24 * a, side * 7 * Mathf.Sin(a * Mathf.PI)),
                        center + scale * new Vector2(-12 + 24 * b, side * 7 * Mathf.Sin(b * Mathf.PI)), 1.7f * scale);
                }
            }
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8, b = (i + 1) * Mathf.PI / 8;
                int start = mesh.currentVertCount;
                Vertex(mesh, center);
                Vertex(mesh, center + 3 * scale * new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
                Vertex(mesh, center + 3 * scale * new Vector2(Mathf.Cos(b), Mathf.Sin(b)));
                mesh.AddTriangle(start, start + 1, start + 2);
            }
            if (passwordVisible)
                Line(mesh, center + scale * new Vector2(-11, 9), center + scale * new Vector2(11, -9), 2 * scale);
        }

        private void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width)
        {
            Vector2 offset = Vector2.Perpendicular(b - a).normalized * width * .5f;
            int start = mesh.currentVertCount;
            Vertex(mesh, a - offset); Vertex(mesh, a + offset);
            Vertex(mesh, b + offset); Vertex(mesh, b - offset);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private void Vertex(VertexHelper mesh, Vector2 position)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            mesh.AddVert(vertex);
        }
    }
}
