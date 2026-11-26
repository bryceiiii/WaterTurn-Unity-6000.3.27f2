using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WaterTurn.Forge
{
    /// <summary>
    /// 极简 UGUI 工厂：全部界面由代码搭建（与项目里 HubGame 的做法一致），
    /// 坐标用「左上角为原点、y 向下」的局部布局，写起来跟设计稿一样直观。
    /// </summary>
    public static class ForgeUi
    {
        static Font uiFont;

        public static Font UiFont
        {
            get
            {
                if (uiFont != null) return uiFont;
                uiFont = Font.CreateDynamicFontFromOSFont(
                    new[] {"Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial"}, 18);
                if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return uiFont;
            }
        }

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(100, 30);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        public static Image Box(Transform parent, string name, Color color, float x, float y, float width, float height,
            bool raycast = false)
        {
            var rt = Place(Node(parent, name), x, y, width, height);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static Text Label(Transform parent, string content, int size, Color color, TextAnchor anchor,
            float x, float y, float width, float height, bool wrap = true)
        {
            var rt = Place(Node(parent, "Label"), x, y, width, height);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = UiFont;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.15f;
            return text;
        }

        public static Button Button(Transform parent, string caption, int fontSize, Color bg, Color fg,
            float x, float y, float width, float height, Action onClick)
        {
            var rt = Place(Node(parent, "Btn_" + caption), x, y, width, height);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = bg;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            Label(rt, caption, fontSize, fg, TextAnchor.MiddleCenter, 4, 0, width - 8, height, false);
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        public static Text TextOf(Button button) => button == null ? null : button.GetComponentInChildren<Text>();

        public static void SetLabel(Button button, string caption)
        {
            var text = TextOf(button);
            if (text != null) text.text = caption;
        }

        public static void SetBg(Button button, Color color)
        {
            if (button != null && button.targetGraphic != null) button.targetGraphic.color = color;
        }

        public static Toggle Toggle(Transform parent, string caption, bool value, Color boxColor, Color checkColor,
            Color fg, float x, float y, float width, float height, Action<bool> onChange)
        {
            var rt = Place(Node(parent, "Tg_" + caption), x, y, width, height);
            var toggle = rt.gameObject.AddComponent<Toggle>();
            var box = Place(Node(rt, "Box"), 0, Mathf.Max(0, (height - 18) * 0.5f), 18, 18);
            var boxImage = box.gameObject.AddComponent<Image>();
            boxImage.color = boxColor;
            var check = Place(Node(box, "Check"), 3, 3, 12, 12);
            var checkImage = check.gameObject.AddComponent<Image>();
            checkImage.color = checkColor;
            checkImage.raycastTarget = false;

            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.transition = Selectable.Transition.None;

            var label = Label(rt, caption, 13, fg, TextAnchor.MiddleLeft, 24, 0, width - 24, height, false);
            label.raycastTarget = true;   // 点文字也能勾上，手不用瞄准小方块

            toggle.isOn = value;
            checkImage.enabled = value;
            toggle.onValueChanged.AddListener(on =>
            {
                checkImage.enabled = on;
                onChange?.Invoke(on);
            });
            return toggle;
        }

        public static InputField Field(Transform parent, string text, string placeholderText, int fontSize,
            Color bg, Color fg, Color dim, float x, float y, float width, float height, bool multiline = false)
        {
            var rt = Place(Node(parent, "Field"), x, y, width, height);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = bg;
            var field = rt.gameObject.AddComponent<InputField>();

            float padX = 10f;
            float padY = multiline ? 8f : 0f;
            var textComponent = Label(rt, "", fontSize, fg,
                multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft,
                padX, padY, width - padX * 2, multiline ? height - padY * 2 : height, multiline);
            textComponent.supportRichText = false;

            var placeholder = Label(rt, placeholderText, fontSize, dim,
                multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft,
                padX, padY, width - padX * 2, multiline ? height - padY * 2 : height, multiline);
            placeholder.supportRichText = false;

            field.targetGraphic = image;
            field.textComponent = textComponent;
            field.placeholder = placeholder;
            field.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            field.caretColor = fg;
            field.selectionColor = new Color(fg.r, fg.g, fg.b, 0.25f);
            field.text = text ?? "";
            return field;
        }

        public static void PaintSegments(IList<Button> segments, int selected, Color idle, Color active,
            Color idleText, Color activeText)
        {
            if (segments == null) return;
            for (int i = 0; i < segments.Count; i++)
            {
                bool on = i == selected;
                SetBg(segments[i], on ? active : idle);
                var text = TextOf(segments[i]);
                if (text != null) text.color = on ? activeText : idleText;
            }
        }

        /// <summary>把一个 RectTransform 铺满父级，用来做面板底。</summary>
        public static RectTransform Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }
    }
}
