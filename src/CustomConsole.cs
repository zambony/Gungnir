using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Consol
{
    internal class CustomConsole : MonoBehaviour
    {
        private const int s_maxHistory = 200;
        private List<string> m_history = new List<string>();
        private List<string> m_commandHistory = new List<string>();
        private int m_historyIndex = 0;
        private Vector2 m_scrollPosition = new Vector2(0, int.MaxValue);
        private const BindingFlags s_bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private string m_currentText = string.Empty;
        private bool m_foundConsoleInstance = false;
        private bool m_foundChatInstance = false;
        private Rect m_windowRect;
        private CommandHandler m_handler;
        private GUIStyle m_consoleStyle = new GUIStyle();
        private const int s_fontSize = 16;
        private const int s_historyEntryMargin = 8;

        public CommandHandler Handler { get => m_handler; set => m_handler = value; }

        private List<string> GetConsoleBuffer()
        {
            return typeof(Console).GetField("m_chatBuffer", s_bindingFlags).GetValue(Console.instance) as List<string>;
        }

        private void SetConsoleBuffer(List<string> buffer)
        {
            typeof(Console).GetField("m_chatBuffer", s_bindingFlags).SetValue(Console.instance, buffer);
        }

        private void Start()
        {
            DontDestroyOnLoad(gameObject);

            CreateStyle();
        }

        private void CreateStyle()
        {
            Font font = Font.CreateDynamicFontFromOSFont("Consolas", s_fontSize);
            m_consoleStyle.wordWrap = true;
            m_consoleStyle.fontSize = font.fontSize;
            m_consoleStyle.font = font;
            m_consoleStyle.normal.textColor = Color.white;
            m_consoleStyle.richText = true;

            if (Console.instance != null)
            {
                // Apply customization to the existing console.
                Canvas canvas = Console.instance.gameObject.GetComponent<Canvas>();

                // Ahh, crispy text.
                if (canvas != null)
                {
                    canvas.pixelPerfect = true;
                    canvas.planeDistance = 0;  // not really necessary since we're going to screenspace, but just in case.
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;  // do this last so the text objects refresh with pixelPerfect enabled.
                }

                // I know you're here somewhere, give me your background component!!!
                Image background = Console.instance.gameObject.GetComponentInChildren<Image>(true);

                if (background != null)
                {
                    background.color = new Color(0.19607843137254902f, 0.21568627450980393f, 0.27058823529411763f, 0.3f);
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

        private void Awake()
        {

        }

        private void SetInputText(string text)
        {
            m_currentText = text;

            if (Console.instance != null)
            {
                Console.instance.m_input.text = text;
                Console.instance.m_input.caretPosition = m_currentText.Length;
            }
        }

        private void OnConsoleDetected()
        {
            m_windowRect = new Rect(7, 1, Console.instance.m_output.rectTransform.rect.width - 7, Console.instance.m_output.rectTransform.rect.height + 1);

            // Create it again because I'm a lazy bastard.
            CreateStyle();
        }

        private void UpdateConsole()
        {
            if (!m_foundConsoleInstance && Console.instance != null)
            {
                m_foundConsoleInstance = true;
                OnConsoleDetected();
            }
            else if (Console.instance == null)
            {
                m_foundConsoleInstance = false;
                return;
            }

            if (!m_foundChatInstance && Chat.instance != null)
            {
                m_foundChatInstance = true;
                OnConsoleDetected();
            }
            else if (Chat.instance == null)
            {
                m_foundChatInstance = false;
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

            if (Console.IsVisible())
            {
                m_currentText = Console.instance.m_input.text;

                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    if (!string.IsNullOrEmpty(m_currentText))
                    {
                        // Log this command to history.
                        m_commandHistory.Add(Util.StripTags(m_currentText));
                        // Delete the currently tracked text.
                        m_currentText = string.Empty;
                        // After a command is entered, cycling to through recent history entries should
                        // start back from the most recent entry. Set to Count, not Count - 1, because the first time we
                        // subtract index it should grab the most recent entry at Count - 1.
                        m_historyIndex = m_commandHistory.Count;
                    }
                }
                // Don't continue going through past entries if they're already at the top.
                // Current console has history already, but eh keep this around.
                //else if (Input.GetKeyDown(KeyCode.UpArrow) && (m_historyIndex - 1) >= 0)
                //{
                //    m_historyIndex = Math.Max(m_historyIndex - 1, 0);
                //    SetInputText(m_commandHistory[m_historyIndex]);
                //}
                //// Same as above, we can't scroll into the future.
                //else if (Input.GetKeyDown(KeyCode.DownArrow) && (m_historyIndex + 1) <= (m_commandHistory.Count - 1))
                //{
                //    m_historyIndex = Math.Min(m_historyIndex + 1, m_commandHistory.Count - 1);
                //    SetInputText(m_commandHistory[m_historyIndex]);
                //}
            }
            else
            {
                m_historyIndex = m_commandHistory.Count;
            }
        }

        private void OnGUI()
        {
            if (Console.instance != null && Console.IsVisible())
            {
                // Not reassigning windowRect because we don't want automatic resizing.
                GUILayout.Window(5001, m_windowRect, DrawConsole, "CONSOL", m_consoleStyle);
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

        private void Update()
        {
            UpdateConsole();
        }
    }
}
