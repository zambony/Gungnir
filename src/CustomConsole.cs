using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace Gungnir
{
    internal class CustomConsole : MonoBehaviour
    {
        private const BindingFlags s_bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const string s_argPattern = @"((?:<[^>]+>)|(?:\[[^\]]+\]))";

        // GUI Style values.
        private const int s_maxHistory         = 1000;
        private const int s_fontSize           = 16;
        private const int s_historyEntryMargin = 10;
        private static Color s_backgroundColor = new Color32(42, 47, 58, 165);
        private GUIStyle  m_consoleStyle       = new GUIStyle();

        // History-related values.
        private List<string> m_history        = new List<string>();

        // GUI drawing variables.
        private Vector2 m_scrollPosition = new Vector2(0, int.MaxValue);
        private string  m_currentText    = string.Empty;
        private int     m_caretPos       = 0;
        private float   m_hintAlpha      = 0f;
        private float   m_hintAlphaVel   = 0f;
        private Rect    m_windowRect;
        private Image   m_background;

        // Command completion/hints.
        private CommandMeta m_currentCommand;
        private string      m_currentHint = string.Empty;

        // Flags.
        private bool m_foundConsoleInstance = false;
        private bool m_foundChatInstance    = false;

        // Link to the command handler.
        private CommandHandler m_handler;
        public CommandHandler  Handler { get => m_handler; set => m_handler = value; }

        public int MaxHistory { get => s_maxHistory; }
        public float Height { get => m_windowRect.height;  }
        public float LineHeight { get => m_consoleStyle.CalcHeight(GUIContent.none, m_windowRect.width); }
        public int VisibleLines { get => (int)(((m_windowRect.height - LineHeight) / (LineHeight + s_historyEntryMargin))); }

        public void ClearScreen()
        {
            IEnumerator clear()
            {
                yield return null;
                m_history.Clear();
                Console.instance.m_output.text = string.Empty;
            }

            m_history.Clear();
            Console.instance.m_output.text = string.Empty;

            // Coroutine to wait a frame and clear later, since this may have come
            // from a console command, which would leave /clear on the screen, etc.
            StartCoroutine(clear());
        }

        /// <summary>
        /// Retrieve the content of the default console's history.
        /// </summary>
        /// <returns><see cref="List{string}"/> of text lines.</returns>
        private List<string> GetConsoleBuffer()
        {
            return typeof(Console).GetField("m_chatBuffer", s_bindingFlags).GetValue(Console.instance) as List<string>;
        }

        private void SetConsoleBuffer(List<string> buffer)
        {
            typeof(Console).GetField("m_chatBuffer", s_bindingFlags).SetValue(Console.instance, buffer);
        }

        public void Print(string text)
        {
            // Delay by a single frame because Valheim runs the command before printing the command you ran.
            // This looks weird if the command is printing values, so...
            IEnumerator print(string value)
            {
                yield return null;
                m_history.Add(value);
            }

            StartCoroutine(print(text));
        }

        private void Start()
        {
            DontDestroyOnLoad(gameObject);

            CreateStyle();
        }

        /// <summary>
        /// Initializes the GUI style object and applies it.
        /// </summary>
        private void CreateStyle()
        {
            Font font = Font.CreateDynamicFontFromOSFont("Consolas", s_fontSize);
            m_consoleStyle.wordWrap = true;
            m_consoleStyle.fontSize = font.fontSize;
            m_consoleStyle.font = font;
            m_consoleStyle.normal.textColor = Color.white;
            m_consoleStyle.richText = true;
            m_consoleStyle.alignment = TextAnchor.UpperLeft;

            if (Console.instance)
            {
                // Apply customization to the existing console.
                Canvas canvas = Console.instance.gameObject.GetComponent<Canvas>();

                // Ahh, crispy text.
                if (canvas)
                {
                    canvas.pixelPerfect = true;
                    canvas.planeDistance = 0;  // not really necessary since we're going to screenspace, but just in case.
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;  // do this last so the text objects refresh with pixelPerfect enabled.
                }

                // I know you're here somewhere, give me your background component!!!
                Image background = Console.instance.gameObject.GetComponentInChildren<Image>(true);

                if (background)
                {
                    m_background = background;
                    background.color = s_backgroundColor;
                }

                Console.instance.m_output.font = font;
                Console.instance.m_output.fontSize = font.fontSize;
                Console.instance.m_output.color = Color.white;
                Console.instance.m_input.textComponent.font = font;
                Console.instance.m_input.textComponent.fontSize = font.fontSize;
                Console.instance.m_input.caretColor = Color.white;
                Console.instance.m_input.customCaretColor = true;

                Console.instance.m_output.gameObject.SetActive(false);
                Console.instance.m_output.text = string.Empty;
            }

            m_history.Clear();
        }

        private void SetInputText(string text)
        {
            m_currentText = text;

            if (Console.instance)
            {
                Console.instance.m_input.text = text;
                Console.instance.m_input.caretPosition = m_currentText.Length;
            }
        }

        private void OnConsoleDetected()
        {
            m_windowRect = new Rect(10, 1, Console.instance.m_output.rectTransform.rect.width - 10, Console.instance.m_output.rectTransform.rect.height);

            // Create it again because I'm a lazy bastard.
            CreateStyle();

            Console.instance.m_input.onValueChanged.RemoveListener(OnInputChanged);
            Console.instance.m_input.onValueChanged.AddListener(OnInputChanged);
        }

        private void OnInputChanged(string text)
        {
            m_currentText = text;

            var split = text.Split();

            if (split.Length == 0)
                return;

            string command = split[0];

            // Don't bother, it's not ours.
            if (!command.StartsWith("/"))
                return;

            m_currentCommand = Handler.GetCommand(command);
        }

        private void UpdateCommandHint()
        {
            if (m_currentCommand == null || string.IsNullOrEmpty(m_currentText))
                return;

            if (string.IsNullOrEmpty(m_currentCommand.hint))
            {
                m_currentHint = $"{("/" + m_currentCommand.data.keyword).WithColor(Logger.WarningColor)}\n{m_currentCommand.data.description}";
                return;
            }

            // Split the hint string into pieces so we can add color or boldness to each one.
            var splitHints = Util.SplitByPattern(m_currentCommand.hint, s_argPattern);
            // Split each argument manually, because we SplitByQuotes does not preserve the quotations, or return the match list.
            // We want the quotation marks so we can see exactly where in the string an argument starts/ends.
            var splitArgs = Regex.Matches(m_currentText, Util.CommandPattern).OfType<Match>().Skip(1).ToArray();

            int currentArg = 0;

            string final = "";

            for (int i = 0; i < splitArgs.Length; ++i)
            {
                var match = splitArgs[i];
                var group = match.Groups[0];
                int groupEnd = group.Index + group.Length;

                // If our caret is before the end of this argument, and the argument the caret is touching
                // is not beyond the number of argument hints we have, select this argument as the one to highlight.
                if (m_caretPos <= groupEnd && i < splitHints.Count)
                {
                    currentArg = i;
                    break;
                }
                // If our caret is past the last character of our current argument (e.g. the user inserted a space
                // after the argument), assume the next argument is what we're targeting, but only if there is a next argument to
                // use.
                else if (m_caretPos > groupEnd && i + 1 < splitHints.Count)
                {
                    currentArg = i + 1;
                }
                // Otherwise, we're probably entering an array argument and typing multiple things, so mark the last argument
                // as the current one.
                else if (i >= splitHints.Count)
                {
                    currentArg = splitHints.Count - 1;
                    break;
                }
            }

            // Apply coloring/highlighting to each hint.
            for (int i = 0; i < splitHints.Count; ++i)
            {
                var hint = splitHints[i];

                if (i == currentArg)
                    final += "<b>" + hint.WithColor(Logger.GoodColor) + "</b> ";
                else
                    final += hint.WithColor(new Color(0.8f, 0.8f, 0.8f)) + " ";
            }

            m_currentHint = $"{("/" + m_currentCommand.data.keyword).WithColor(Logger.WarningColor)} {final.Trim()}\n{m_currentCommand.data.description}";
        }

        private void Update()
        {
            if (!m_foundConsoleInstance && Console.instance)
            {
                m_foundConsoleInstance = true;
                OnConsoleDetected();
            }
            else if (!Console.instance)
            {
                m_foundConsoleInstance = false;
                return;
            }

            if (!m_foundChatInstance && Chat.instance)
            {
                m_foundChatInstance = true;
                OnConsoleDetected();
            }
            else if (!Chat.instance)
            {
                m_foundChatInstance = false;
            }

            if (m_caretPos != Console.instance.m_input.caretPosition)
            {
                m_caretPos = Console.instance.m_input.caretPosition;
                UpdateCommandHint();
            }

            List<string> currentBuffer = GetConsoleBuffer();

            if (currentBuffer.Count > 0)
            {
                m_history.AddRange(currentBuffer);
                currentBuffer.Clear();

                // Chop off anything outside our max history limit.
                if (m_history.Count > s_maxHistory)
                    m_history.RemoveRange(0, Math.Abs(s_maxHistory - m_history.Count));

                Console.instance.m_output.text = string.Empty;
                m_scrollPosition = new Vector2(0, int.MaxValue);
            }
        }

        private void OnGUI()
        {
            if (Console.IsVisible())
            {
                // Not reassigning windowRect because we do not want to allow the user to drag the window.
                GUILayout.Window(5001, m_windowRect, DrawConsole, "Gungnir " + Gungnir.ModVersion, m_consoleStyle);

                float curAlpha = m_hintAlpha;

                if (m_currentCommand == null || string.IsNullOrEmpty(m_currentText))
                    m_hintAlpha = Mathf.SmoothDamp(m_hintAlpha, 0f, ref m_hintAlphaVel, 0.2f);
                else
                    m_hintAlpha = Mathf.SmoothDamp(m_hintAlpha, 1f, ref m_hintAlphaVel, 0.2f);

                if (Mathf.Approximately(m_hintAlpha, 0f))
                    return;

                // Please pay no attention to the fact I'm regex replacing the contents of a string every frame.
                // Unity UI work is painfully bad to do through code and the only thing that can affect the alpha of colored RichText
                // is the alpha byte of RichText color tags. This is the only spot I'll do this, so forgive me D:
                GUIContent content;
                if (m_hintAlpha != curAlpha)
                    content = new GUIContent(Util.MultiplyColorTagAlpha(m_currentHint, m_hintAlpha));
                else
                    content = new GUIContent(m_currentHint);

                Color oldColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, m_hintAlpha);
                // I don't want to deal with auto layout stuff, so I will draw the command help exactly
                // where I want it :)
                Vector2 contentSize = m_consoleStyle.CalcSize(content);
                float height = m_consoleStyle.CalcHeight(content, contentSize.x);
                const float margin = 10f;

                GUITools.DrawRect(new Rect(m_windowRect.x - margin, m_background.rectTransform.rect.height + 30f, contentSize.x + (margin * 2), height + (margin * 2)), s_backgroundColor);
                GUI.Label(
                    new Rect(m_windowRect.x, m_background.rectTransform.rect.height + 30f + margin, m_windowRect.width, LineHeight * 2f),
                    content,
                    m_consoleStyle
                );
                GUI.color = oldColor;
            }
            else
            {
                m_hintAlpha = 0f;
            }
        }

        private void DrawConsole(int windowID)
        {
            if (!Cursor.visible)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            GUILayout.Space(s_fontSize);

            // Push content to the bottom of the console until the scroll history is big enough.
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical();

            m_scrollPosition = GUILayout.BeginScrollView(m_scrollPosition);

            foreach (string line in m_history)
            {
                GUILayout.Label(line, m_consoleStyle);
                GUILayout.Space(s_historyEntryMargin);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }
    }

    public static class GUITools
    {
        private static readonly Texture2D backgroundTexture = Texture2D.whiteTexture;
        private static readonly GUIStyle textureStyle = new GUIStyle { normal = new GUIStyleState { background = backgroundTexture } };

        public static void DrawRect(Rect position, Color color, GUIContent content = null)
        {
            var backgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUI.Box(position, content ?? GUIContent.none, textureStyle);
            GUI.backgroundColor = backgroundColor;
        }

        public static void LayoutBox(Color color, GUIContent content = null)
        {
            var backgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Box(content ?? GUIContent.none, textureStyle);
            GUI.backgroundColor = backgroundColor;
        }
    }
}
