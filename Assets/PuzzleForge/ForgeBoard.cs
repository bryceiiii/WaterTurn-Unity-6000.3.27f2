using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn.Forge
{
    /// <summary>
    /// 盘面渲染器：把 rows（ASCII 盘面）画成一格一格的 UGUI 方块。
    /// 手动编辑区用它接点击，预览区用它只读显示（试玩时喂的是实时 Position）。
    /// </summary>
    public sealed class ForgeBoard : MonoBehaviour
    {
        public Action<int, int> CellClicked;

        RectTransform body;
        Image backing;
        readonly List<Image> cells = new List<Image>();
        readonly List<Text> glyphs = new List<Text>();
        int columns, rows;
        float cell, gap = 2f, areaWidth, areaHeight;
        bool interactive;

        public int Columns => columns;
        public int Rows => rows;

        public static ForgeBoard Create(Transform parent, string name, float x, float y, float width, float height,
            bool interactive, Action<int, int> onClick)
        {
            var rt = ForgeUi.Place(ForgeUi.Node(parent, name), x, y, width, height);
            var board = rt.gameObject.AddComponent<ForgeBoard>();
            board.areaWidth = width;
            board.areaHeight = height;
            board.interactive = interactive;
            board.CellClicked = onClick;
            return board;
        }

        public void Render(string[] data, ForgePalette palette)
        {
            if (data == null || data.Length == 0) return;
            int width = 0;
            for (int i = 0; i < data.Length; i++)
                if (data[i] != null && data[i].Length > width)
                    width = data[i].Length;
            if (width == 0) return;

            if (body == null || columns != width || rows != data.Length) Build(width, data.Length);
            if (backing != null) backing.color = palette.bg;

            for (int y = 0; y < rows; y++)
            {
                string row = y < data.Length ? data[y] : null;
                for (int x = 0; x < columns; x++)
                {
                    char tile = row != null && x < row.Length ? row[x] : '#';
                    var style = ForgeTiles.Style(tile, palette);
                    int index = y * columns + x;
                    cells[index].color = style.bg;
                    var glyph = glyphs[index];
                    glyph.text = style.glyph;
                    glyph.color = style.fg;
                }
            }
        }

        /// <summary>
        /// 把「当前局面」还原成一份盘面字符。必须以静态地形为底重建：开局的鱼、箱子、水
        /// 都会挪位置，直接在 Level.Tiles 上叠加会留下残影——旋转后看到两条鱼就是这么来的。
        /// </summary>
        public static string[] RowsFrom(Level level, Position position)
        {
            var chars = level.Tiles.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                switch (chars[i])
                {
                    case 'P':   // 鱼的开局格本来就是地面
                    case 'B':   // 干燥地面上的箱子
                    case 'b':   // 水里的箱子
                    case '~':   // 开局的水会流走
                        chars[i] = '.';
                        break;
                }
            }
            if (position.Pearl && level.Pearl >= 0 && level.Pearl < chars.Length)
                chars[level.Pearl] = '.';   // 珍珠被拿走后不该还画在盘面上

            foreach (int cell in position.Water)
            {
                if (cell < 0 || cell >= chars.Length) continue;
                char tile = chars[cell];
                // 出口 / 珍珠 / 泉眼压在水上时仍然画它们自己，其余格子都是水。
                if (tile == 'E' || tile == 'K' || tile == 'o') continue;
                chars[cell] = '~';
            }

            foreach (int box in position.Boxes)
            {
                if (box < 0 || box >= chars.Length) continue;
                chars[box] = chars[box] == '~' ? 'b' : 'B';
            }

            if (position.Fish >= 0 && position.Fish < chars.Length)
                chars[position.Fish] = 'P';

            int width = level.Width;
            var rowsOut = new string[level.Height];
            for (int y = 0; y < level.Height; y++)
                rowsOut[y] = new string(chars, y * width, width);
            return rowsOut;
        }

        public void Clear()
        {
            if (body != null)
            {
                body.gameObject.SetActive(false);
                Destroy(body.gameObject);
                body = null;
            }
            cells.Clear();
            glyphs.Clear();
            columns = rows = 0;
        }

        void Build(int width, int height)
        {
            Clear();
            columns = width;
            rows = height;
            cell = Mathf.Max(10f, Mathf.Floor(Mathf.Min(areaWidth / width, areaHeight / height)));
            float totalWidth = cell * width, totalHeight = cell * height;

            body = ForgeUi.Node(transform, "Cells");
            ForgeUi.Place(body, (areaWidth - totalWidth) * 0.5f, (areaHeight - totalHeight) * 0.5f, totalWidth, totalHeight);
            backing = ForgeUi.Box(body, "Backing", Color.black, 0, 0, totalWidth, totalHeight);

            float size = Mathf.Max(1f, cell - gap);
            int fontSize = Mathf.Clamp(Mathf.RoundToInt(size * 0.46f), 9, 24);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var rt = ForgeUi.Place(ForgeUi.Node(body, "Cell"), x * cell, y * cell, size, size);
                    var image = rt.gameObject.AddComponent<Image>();
                    image.color = Color.black;
                    image.raycastTarget = interactive;
                    cells.Add(image);
                    glyphs.Add(ForgeUi.Label(rt, "", fontSize, Color.white, TextAnchor.MiddleCenter, 0, 0, size, size, false));

                    if (!interactive) continue;
                    int cellX = x, cellY = y;
                    var button = rt.gameObject.AddComponent<Button>();
                    button.targetGraphic = image;
                    button.transition = Selectable.Transition.None;
                    button.onClick.AddListener(() => CellClicked?.Invoke(cellX, cellY));
                }
            }
        }
    }
}
