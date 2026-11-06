using UnityEngine;

public abstract class MazeInterface : MonoBehaviour
{
    protected static readonly Color Ink = new Color(0.067f, 0.059f, 0.056f);
    protected static readonly Color Cream = new Color(0.87f, 0.81f, 0.69f);
    protected static readonly Color Copper = new Color(0.61f, 0.48f, 0.31f);
    protected static readonly Color Line = new Color(0.30f, 0.235f, 0.167f);
    GUIStyle buttonStyle;

    protected void OnGUI()
    {
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        if (scale <= 0) return;
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        int previousDepth = GUI.depth;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        GUI.color = Color.white;
        GUI.depth = -100;
        try
        {
            Draw(Screen.width / scale, Screen.height / scale);
        }
        finally
        {
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }
    }

    protected abstract void Draw(float width, float height);

    protected static GUIStyle LabelStyle(int size, TextAnchor alignment, Color color)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = FontStyle.Bold,
            alignment = alignment,
            wordWrap = false,
            padding = new RectOffset()
        };
        style.normal.textColor = color;
        return style;
    }

    protected bool Button(Rect rect, string label, bool primary = false)
    {
        if (buttonStyle == null)
        {
            buttonStyle = LabelStyle(23, TextAnchor.MiddleCenter, Cream);
            buttonStyle.hover.textColor = Cream;
            buttonStyle.active.textColor = Cream;
            buttonStyle.focused.textColor = Cream;
        }

        bool hover = rect.Contains(Event.current.mousePosition);
        Color background = primary ? new Color(0.23f, 0.176f, 0.118f) : new Color(0.12f, 0.105f, 0.09f);
        if (hover) background = Color.Lerp(background, Copper, 0.22f);
        Fill(rect, hover || primary ? Copper : Line);
        Fill(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), background);
        return GUI.Button(rect, label, buttonStyle);
    }

    protected static void Fill(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
