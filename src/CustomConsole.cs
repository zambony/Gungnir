using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using HarmonyLib;

namespace Consol
{
    internal class CustomConsole : MonoBehaviour
    {
        private const BindingFlags s_bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        // GUI Style values.
        private const int s_maxHistory = 200;
        private const int s_fontSize = 16;
        private const int s_historyEntryMargin = 8;
        private GUIStyle m_consoleStyle = new GUIStyle();

        // History-related value.
        private List<string> m_history = new List<string>();
        private List<string> m_commandHistory = new List<string>();

        // GUI drawing variables.
        private Vector2 m_scrollPosition = new Vector2(0, int.MaxValue);
        private string m_currentText = string.Empty;
        private Rect m_windowRect;

        // Flags.
        private bool m_foundConsoleInstance = false;
        private bool m_foundChatInstance = false;

        // Link to the command handler.
        private CommandHandler m_handler;
        public CommandHandler Handler { get => m_handler; set => m_handler = value; }

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
                    ColorUtility.TryParseHtmlString("#2A2F3A", out Color color);
                    color.a = 0.65f;
                    background.color = color;
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
        }

        private void UpdateConsole()
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
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (Console.IsVisible())
            {
                // Not reassigning windowRect because we do not want to allow the user to drag the window.
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

        private void Update()
        {
            UpdateConsole();
        }
    }
}
