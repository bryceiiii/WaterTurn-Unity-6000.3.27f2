using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn
{
    /// <summary>
    /// The wobbling jelly body.  A MaskableGraphic (not a sprite) because the
    /// HTML prototype's jelly is a live wavy surface with a vertical body
    /// gradient; baking it would lose the squash and the surface line.  The mesh
    /// is rebuilt each frame from the same sine formula as the prototype.
    /// </summary>
    public sealed class JellyGraphic : MaskableGraphic
    {
        public float time;
        public float squash;
        public float seed;
        public float glow;
        public bool blob;

        public Color32 surfaceColor = new Color32(225, 255, 250, 190);
        public Color32 topColor = new Color32(186, 255, 238, 90);
        public Color32 upperColor = new Color32(120, 244, 218, 74);
        public Color32 midColor = new Color32(58, 196, 192, 62);
        public Color32 deepColor = new Color32(22, 104, 128, 100);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float w = r.width, h = r.height;
            if (w < 2f || h < 2f) return;

            int cols = Mathf.Clamp(Mathf.RoundToInt(w / 8f), 6, 80);
            float wob = 4f + squash * 8f;

            // Five rows from the wavy top to the rounded bottom.  Each row has
            // its own width factor so blob jellies taper like a soft capsule.
            float[] rows = {0f, .07f, .42f, .76f, 1f};
            float[] halfWide = blob
                ? new[] {.5f, .5f, .49f, .44f, .30f}
                : new[] {.5f, .5f, .5f, .47f, .43f};

            float SurfaceY(float p)
            {
                return Mathf.Sin(p * 7.4f + time * 2f + seed) * wob * .6f
                       + Mathf.Sin(p * 3.6f - time * 1.4f + seed * .7f) * wob * 1.1f
                       - squash * 3f;
            }

            Color32[] colors = {surfaceColor, topColor, upperColor, midColor, deepColor};
            float cx = r.center.x, top = r.yMax, bottom = r.yMin;

            int vertices = (cols + 1) * rows.Length;
            for (int j = 0; j < rows.Length; j++)
            {
                float t = rows[j];
                for (int i = 0; i <= cols; i++)
                {
                    float p = i / (float)cols;
                    float x = cx + (p - .5f) * w * halfWide[j];
                    float surface = top - SurfaceY(p);
                    float y = Mathf.Lerp(surface, bottom, t);
                    if (j == 0) y = surface;

                    var v = UIVertex.simpleVert;
                    v.position = new Vector3(x, y, 0f);
                    Color32 c = colors[j];
                    if (j == 0)
                    {
                        c.a = (byte)Mathf.Clamp(surfaceColor.a + glow * 45f, 0, 255);
                    }
                    else if (j == 1)
                    {
                        c.a = (byte)Mathf.Clamp(topColor.a + glow * 35f, 0, 255);
                    }
                    v.color = c;
                    vh.AddVert(v);
                }
            }

            for (int j = 0; j < rows.Length - 1; j++)
            {
                int row = j * (cols + 1);
                int next = (j + 1) * (cols + 1);
                for (int i = 0; i < cols; i++)
                {
                    vh.AddTriangle(row + i, row + i + 1, next + i);
                    vh.AddTriangle(row + i + 1, next + i + 1, next + i);
                }
            }
        }
    }
}
