using UnityEngine;
using VContainer;

namespace Play
{
    /// <summary>Draws a prefab-authored net while retaining the scene puzzle and its completion subscription.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DiamondNetView : UnityEngine.UI.MaskableGraphic
    {
        [SerializeField] private Vector2[] _faceVertices = new Vector2[24];
        [SerializeField] private Color[] _palette = new Color[8];
        [SerializeField] private float _stickerInset = 0.04f;
        [Tooltip("Choose DiamondNetStrip, DiamondNetBranch or DiamondNet. Only the prefab's authored geometry is used; puzzle state and HUD placement stay unchanged.")]
        [SerializeField] private DiamondNetView _layoutPrefab;
        private DiamondTwistController _controller;

        /// <summary>Receives the scene puzzle through the existing game scope, never a camera or scene lookup.</summary>
        [Inject]
        public void Construct(DiamondTwistController controller)
        {
            Unsubscribe();
            _controller = controller;
            if (isActiveAndEnabled)
            {
                _controller.TurnCompleted += RefreshColors;
            }

            RefreshColors();
        }

        /// <summary>Rebinds after a HUD hide/show and immediately shows the latest committed state.</summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            if (_controller != null)
            {
                _controller.TurnCompleted += RefreshColors;
            }

            RefreshColors();
        }

        /// <summary>Releases the puzzle subscription while the HUD is hidden or destroyed.</summary>
        protected override void OnDisable()
        {
            Unsubscribe();
            base.OnDisable();
        }

        /// <summary>Detaches only this view's completion listener.</summary>
        private void Unsubscribe()
        {
            if (_controller != null)
            {
                _controller.TurnCompleted -= RefreshColors;
            }
        }

        /// <summary>Schedules a redraw without reading intermediate mesh transforms.</summary>
        private void RefreshColors() => SetVerticesDirty();

        /// <summary>Subdivides every authored face into its center and three axis-corner stickers.</summary>
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper helper)
        {
            helper.Clear();
            if (_faceVertices.Length != 24 || _palette.Length != 8)
            {
                return;
            }

            Rect rect = GetPixelAdjustedRect();
            Vector2[] vertices = _layoutPrefab == null ? _faceVertices : _layoutPrefab._faceVertices;
            for (int face = 0; face < 8; face++)
            {
                Vector2 x = ToLocal(vertices[face * 3], rect);
                Vector2 y = ToLocal(vertices[face * 3 + 1], rect);
                Vector2 z = ToLocal(vertices[face * 3 + 2], rect);
                Vector2 xy = (x + y) / 2f;
                Vector2 yz = (y + z) / 2f;
                Vector2 zx = (z + x) / 2f;
                AddTriangle(helper, x, y, z, new Color(0.04f, 0.05f, 0.07f, 0.95f), 0f);
                AddSticker(helper, face, 0, xy, yz, zx);
                AddSticker(helper, face, 1, x, xy, zx);
                AddSticker(helper, face, 2, y, yz, xy);
                AddSticker(helper, face, 3, z, zx, yz);
            }
        }

        /// <summary>Resolves a fixed slot's color identity through the palette sampled from the model UVs.</summary>
        private void AddSticker(UnityEngine.UI.VertexHelper helper, int face, int slot, Vector2 a, Vector2 b, Vector2 c)
        {
            int identity = _controller == null ? face : _controller.NetState.GetColor(face, slot);
            AddTriangle(helper, a, b, c, _palette[identity] * color, _stickerInset);
        }

        /// <summary>Maps normalized authored coordinates into the anchored RectTransform.</summary>
        private static Vector2 ToLocal(Vector2 point, Rect rect) => rect.min + Vector2.Scale(point, rect.size);

        /// <summary>Adds a consistently wound triangle, inset around its own centroid to expose seams.</summary>
        private static void AddTriangle(UnityEngine.UI.VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Color tint, float inset)
        {
            Vector2 center = (a + b + c) / 3f;
            int start = helper.currentVertCount;
            helper.AddVert(Vector2.Lerp(a, center, inset), tint, Vector2.zero);
            helper.AddVert(Vector2.Lerp(b, center, inset), tint, Vector2.zero);
            helper.AddVert(Vector2.Lerp(c, center, inset), tint, Vector2.zero);
            float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            helper.AddTriangle(start, start + (cross < 0 ? 1 : 2), start + (cross < 0 ? 2 : 1));
        }
    }
}
