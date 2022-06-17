using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEngine;

namespace Gungnir
{
    /// <summary>
    /// Attribute to be applied to a method for use by the command handler.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    internal class Command : Attribute
    {
        public readonly string keyword;
        public readonly string description;
        public readonly string autoCompleteTarget;

        public Command(string keyword, string description, string autoComplete = null)
        {
            this.keyword = keyword;
            this.description = description;
            this.autoCompleteTarget = autoComplete;
        }
    }

    /// <summary>
    /// Stores metadata information about a command, including a reference to the method in question.
    /// </summary>
    internal class CommandMeta
    {
        public delegate List<string> AutoCompleteDelegate();

        /// <summary>
        /// <see cref="Command"/> attribute data to access the command name and description.
        /// </summary>
        public readonly Command data;
        /// <summary>
        /// The actual command function.
        /// </summary>
        public readonly MethodBase method;
        /// <summary>
        /// A <see cref="List{ParameterInfo}"/> of argument information.
        /// </summary>
        public readonly List<ParameterInfo> arguments;
        /// <summary>
        /// A <see langword="string"/> representing argument types, names, and whether they are required
        /// parameters, e.g. <c>&lt;number amount&gt; [Player player]</c>
        /// </summary>
        public readonly string hint;
        /// <summary>
        /// Number of required arguments for the command to run.
        /// </summary>
        public readonly int requiredArguments;
        /// <summary>
        /// Delegate to return potential autocomplete topics.
        /// </summary>
        public readonly AutoCompleteDelegate AutoComplete;

        public CommandMeta(Command data, MethodBase method, List<ParameterInfo> arguments, AutoCompleteDelegate autoCompleteDelegate = null)
        {
            this.data = data;
            this.method = method;
            this.arguments = arguments;
            this.AutoComplete = autoCompleteDelegate;

            // If we have any arguments, attempt to build the argument hint string.
            if (arguments.Count > 0)
            {
                StringBuilder builder = new StringBuilder();

                foreach (ParameterInfo info in arguments)
                {
                    bool optional = info.HasDefaultValue;

                    requiredArguments += optional ? 0 : 1;

                    // Required parameters use chevrons, and optionals use brackets.
                    if (!optional)
                    {
                        builder.Append($"<{Util.GetSimpleTypeName(info.ParameterType)} {info.Name}> ");
                    }
                    else
                    {
                        string defaultValue = info.DefaultValue == null ? "none" : info.DefaultValue.ToString();
                        builder.Append($"[{Util.GetSimpleTypeName(info.ParameterType)} {info.Name}={defaultValue}] ");
                    }
                }

                // Remove trailing space.
                builder.Remove(builder.Length - 1, 1);

                hint = builder.ToString();
            }
            else
            {
                requiredArguments = 0;
            }
        }
    }

    /// <summary>
    /// This class is responsible for parsing and running commands. All commands are
    /// defined here as public methods and annotated with the <see cref="Command"/> attribute.
    /// </summary>
    internal class CommandHandler
    {
        private Dictionary<string, CommandMeta> m_actions = new Dictionary<string, CommandMeta>();
        private const BindingFlags s_bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private CustomConsole m_console = null;
        private Gungnir m_plugin = null;

        internal CustomConsole Console { get => m_console; set => m_console = value; }
        internal Gungnir Plugin { get => m_plugin; set => m_plugin = value; }

        /// <summary>
        /// Helper function for the give command's autocomplete feature.
        /// </summary>
        /// <returns>A <see cref="List{string}"/> of all prefab names if the Scene is loaded, otherwise <see langword="null"/>.</returns>
        private static List<string> GetPrefabNames()
        {
            return ZNetScene.instance?.GetPrefabNames();
        }

        [Command("bind", "Bind a console command to a key. See the Unity documentation for KeyCode names.")]
        public void Bind(string keyCode, params string[] commandText)
        {
            if (!Enum.TryParse(keyCode, true, out KeyCode result))
            {
                Logger.Error($"Couldn't find a key code named {keyCode.WithColor(Color.white)}.", true);
                return;
            }

            if (Plugin.Binds.ContainsKey(result))
                Plugin.Binds.Remove(result);

            string cmd = string.Join(" ", commandText);
            Plugin.Binds.Add(result, cmd);

            Plugin.SaveBinds();

            Logger.Log($"Bound {result.ToString().WithColor(Logger.GoodColor)} to {cmd.WithColor(Logger.WarningColor)}.", true);
        }

        [Command("butcher", "Kills all living creatures within a radius, excluding players.")]
        public void KillAll(float radius = 50f, bool killTamed = false)
        {
            List<Character> characters = new List<Character>();
            Character.GetCharactersInRange(Player.m_localPlayer.transform.position, radius, characters);

            int count = 0;
            foreach (Character character in characters)
            {
                if (!character.IsPlayer() && !(character.IsTamed() && !killTamed))
                {
                    HitData hitData = new HitData();
                    hitData.m_damage.m_damage = Mathf.Infinity;
                    character.Damage(hitData);
                    ++count;
                }
            }

            Logger.Log($"Murdered {count.ToString().WithColor(Logger.GoodColor)} creatures within {radius.ToString().WithColor(Logger.GoodColor)} meters.", true);
        }

        [Command("clear", "Clears the console's output.")]
        public void ClearConsole()
        {
            Console?.ClearScreen();
        }

        [Command("creative", "Toggles creative mode, which removes the need for resources.")]
        public void ToggleNoCost()
        {
            bool enabled = Player.m_localPlayer.ToggleNoPlacementCost();

            if (!enabled && !Player.m_localPlayer.IsDebugFlying())
                Player.m_debugMode = false;
            else
                Player.m_debugMode = true;

            Logger.Log($"Creative mode: {(enabled ? "ON".WithColor(Logger.GoodColor) : "OFF".WithColor(Logger.ErrorColor))}", true);
        }

        [Command("echo", "Shout into the void.")]
        public void Echo(params string[] values)
        {
            global::Console.instance.Print(string.Join(" ", values));
        }

        [Command("fly", "Toggles the ability to fly.")]
        public void ToggleFly()
        {
            bool enabled = Player.m_localPlayer.ToggleDebugFly();

            if (!enabled && !Player.m_localPlayer.NoCostCheat())
                Player.m_debugMode = false;
            else
                Player.m_debugMode = true;

            Logger.Log($"Flight: {(enabled ? "ON".WithColor(Logger.GoodColor) : "OFF".WithColor(Logger.ErrorColor))}", true);
        }

        [Command("give", "Give an item to yourself or another player.", nameof(GetPrefabNames))]
        public void Give(string itemName, int amount = 1, Player player = null, int level = 1)
        {
            if (!ZNetScene.instance)
            {
                Logger.Error("No scene found. Try loading a world.", true);
                return;
            }

            if (string.IsNullOrEmpty(itemName))
            {
                Logger.Error("You must specify a name.", true);
                return;
            }

            if (amount <= 0)
            {
                Logger.Error("Amount must be greater than 0.", true);
                return;
            }

            if (level <= 0)
            {
                Logger.Error("Level must be greater than 0.", true);
                return;
            }

            if (player == null)
                player = Player.m_localPlayer;


            GameObject prefabObject;

            try
            {
                prefabObject = Util.GetPrefabByName(itemName);
            }
            catch (TooManyValuesException)
            {
                Logger.Error($"Found more than one prefab containing the text <color=white>{itemName}</color>, please be more specific.", true);
                return;
            }
            catch (NoMatchFoundException)
            {
                Logger.Error($"Couldn't find any prefabs named <color=white>{itemName}</color>.", true);
                return;
            }

            if (prefabObject.GetComponent<ItemDrop>() == null)
            {
                Logger.Error($"Found a prefab named <color=white>{itemName}</color>, but that isn't an item.", true);
                return;
            }

            Vector3 vector = UnityEngine.Random.insideUnitSphere * 0.5f;
            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Spawning object " + prefabObject.name);

            GameObject spawned = UnityEngine.Object.Instantiate(prefabObject, player.transform.position + player.transform.forward * 2f + Vector3.up + vector, Quaternion.identity);

            if (!spawned)
            {
                Logger.Error("Something went wrong!", true);
                return;
            }

            ItemDrop item = spawned.GetComponent<ItemDrop>();

            item.m_itemData.m_quality = level;
            item.m_itemData.m_stack = amount;
            item.m_itemData.m_durability = item.m_itemData.GetMaxDurability();

            Logger.Log(
                $"Gave {amount.ToString().WithColor(Logger.GoodColor)} of {prefabObject.name.WithColor(Logger.GoodColor)} to {player.GetPlayerName().WithColor(Logger.GoodColor)}.", true);

            player.Pickup(spawned, autoequip: false, autoPickupDelay: false);
        }

        [Command("ghost", "Toggles ghost mode. Prevents hostile creatures from detecting you.")]
        public void ToggleGhostMode()
        {
            Player.m_localPlayer.SetGhostMode(!Player.m_localPlayer.InGhostMode());
            Logger.Log($"Ghost mode: {(Player.m_localPlayer.InGhostMode() ? "ON".WithColor(Logger.GoodColor) : "OFF".WithColor(Logger.ErrorColor))}", true);
        }

        [Command("god", "Toggles invincibility.")]
        public void ToggleGodmode()
        {
            Player.m_localPlayer.SetGodMode(!Player.m_localPlayer.InGodMode());
            Logger.Log($"God mode: {(Player.m_localPlayer.InGodMode() ? "ON".WithColor(Logger.GoodColor) : "OFF".WithColor(Logger.ErrorColor))}", true);
        }

        [Command("heal", "Heal all of your wounds.")]
        public void Heal()
        {
            Player.m_localPlayer.Heal(Player.m_localPlayer.GetMaxHealth(), true);
            Logger.Log("All wounds cured.", true);
        }

        [Command("help", "Prints the command list, or looks up the syntax of a specific command. Also accepts a page number, in case of many commands.")]
        public void Help(string commandOrPageNum = null)
        {
            int pageNum = 1;
            bool wantsPage = int.TryParse(commandOrPageNum, out pageNum);
            pageNum = Math.Max(pageNum, 1);

            if (commandOrPageNum == null || wantsPage)
            {
                int usableLines = Console.VisibleLines - 2;  // subtracting 2 because we print out the Page counter and a newline.
                int totalPages = Math.Max((int)Math.Ceiling((m_actions.Count * 3f) / usableLines), 1);
                int entriesPerPage = (int)Math.Ceiling(usableLines / 3f);

                if (pageNum > totalPages)
                {
                    Logger.Error($"Max number of help pages is {totalPages}.", true);
                    return;
                }

                if (!wantsPage || pageNum == 1)
                    global::Console.instance.Print($"\n[Gungnir] Version {Gungnir.ModVersion} by {Gungnir.ModOrg}\n");

                if (wantsPage)
                    global::Console.instance.Print($"Page ({pageNum}/{totalPages})\n");

                int pageStart = (pageNum - 1) * entriesPerPage;

                var cmds =
                    m_actions.Values.ToList()
                    .Skip(pageStart)
                    .Take(wantsPage ? entriesPerPage : m_actions.Count)
                    .OrderBy(m => m.data.keyword);

                foreach (CommandMeta meta in cmds)
                {
                    string fullCommand = "/" + meta.data.keyword;
                    // Not using logger because this doesn't need to be logged.
                    if (meta.arguments.Count > 0)
                        global::Console.instance.Print($"{fullCommand.WithColor(Logger.WarningColor)} {meta.hint.WithColor(Logger.GoodColor)}\n{meta.data.description}");
                    else
                        global::Console.instance.Print($"{fullCommand.WithColor(Logger.WarningColor)}\n{meta.data.description}");

                    global::Console.instance.Print("");
                }
            }
            else
            {
                // Add the command prefix if they didn't add it.
                if (!commandOrPageNum.StartsWith("/"))
                    commandOrPageNum = "/" + commandOrPageNum;

                if (!m_actions.TryGetValue(commandOrPageNum, out var meta))
                {
                    Logger.Error($"Sorry, couldn't find a command named {commandOrPageNum.WithColor(Color.white)}.", true);
                    return;
                }

                // Spacing between command text and output, for readability.
                global::Console.instance.Print("");

                if (meta.arguments.Count > 0)
                    global::Console.instance.Print($"{commandOrPageNum.WithColor(Logger.WarningColor)} {meta.hint.WithColor(Logger.GoodColor)}\n{meta.data.description}");
                else
                    global::Console.instance.Print($"{commandOrPageNum.WithColor(Logger.WarningColor)}\n{meta.data.description}");

            }
        }

        [Command("listbinds", "List all of your custom keybinds, or check what an individual keycode is bound to.")]
        public void ListBinds(string keyCode = null)
        {
            if (Plugin.Binds.Count == 0)
            {
                Logger.Error($"You have no keybinds currently set. Use {"/bind".WithColor(Color.white)} to add some.", true);
                return;
            }

            if (string.IsNullOrEmpty(keyCode))
            {
                foreach (KeyValuePair<KeyCode, string> pair in Plugin.Binds)
                    global::Console.instance.Print($"{pair.Key} = {pair.Value.WithColor(Logger.WarningColor)}");

                return;
            }

            if (!Enum.TryParse(keyCode, true, out KeyCode result))
            {
                Logger.Error($"Couldn't find a key code named {keyCode.WithColor(Color.white)}.", true);
                return;
            }

            if (!Plugin.Binds.TryGetValue(result, out string cmd))
            {
                Logger.Error($"{keyCode.ToString().WithColor(Color.white)} is not bound to anything.", true);
                return;
            }

            global::Console.instance.Print($"{result} = {cmd.WithColor(Logger.WarningColor)}");
        }

        [Command("listweather", "Get a list of all available weather types.")]
        public void ListWeather()
        {
            if (!EnvMan.instance)
            {
                Logger.Error("No scene found. Try loading a world.", true);
                return;
            }

            var query =
                from env in EnvMan.instance.m_environments
                select env.m_name;

            Logger.Log($"Found {query.Count().ToString().WithColor(Logger.GoodColor)} available weather types...", true);

            foreach (string name in query)
                global::Console.instance.Print(name);
        }

        [Command("listitems", "List every item in the game, or search for one that contains your text.")]
        public void ListItems(string itemName = null)
        {
            if (!ZNetScene.instance)
            {
                Logger.Error("No scene found. Try loading a world.", true);
                return;
            }

            List<string> foundItems = new List<string>();

            foreach (string prefabName in ZNetScene.instance.GetPrefabNames())
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);

                ItemDrop item = prefab.GetComponent<ItemDrop>();

                if (!item)
                    continue;

                if (itemName == null)
                {
                    foundItems.Add(prefabName);
                    continue;
                }

                int index = prefabName.IndexOf(itemName, StringComparison.OrdinalIgnoreCase);

                if (index == -1) continue;

                string matchSub = prefabName.Substring(index, itemName.Length);
                string match = prefabName.Replace(matchSub, matchSub.WithColor(Logger.GoodColor));
                foundItems.Add(match);
            }

            if (foundItems.Count == 0)
                Logger.Error($"Couldn't find any items containing the text {itemName.WithColor(Color.white)}.");
            else
            {
                Logger.Log($"Found {foundItems.Count.ToString().WithColor(Logger.GoodColor)} items...", true);

                foreach (string item in foundItems)
                    global::Console.instance.Print(item);
            }
        }

        [Command("listprefabs", "List every prefab in the game, or search for one that contains your text.")]
        public void ListPrefabs(string prefabName = null)
        {
            if (!ZNetScene.instance)
            {
                Logger.Error("No scene found. Try loading a world.", true);
                return;
            }

            List<string> foundPrefabs = new List<string>();

            foreach (string name in ZNetScene.instance.GetPrefabNames())
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(name);

                if (prefabName == null)
                {
                    foundPrefabs.Add(name);
                    continue;
                }

                int index = name.IndexOf(prefabName, StringComparison.OrdinalIgnoreCase);

                if (index == -1)
                    continue;

                string matchSub = name.Substring(index, prefabName.Length);
                string match = name.Replace(matchSub, matchSub.WithColor(Logger.GoodColor));
                foundPrefabs.Add(match);
            }

            if (foundPrefabs.Count == 0)
                Logger.Error($"Couldn't find any items containing the text {prefabName.WithColor(Color.white)}.");
            else
            {
                Logger.Log($"Found {foundPrefabs.Count.ToString().WithColor(Logger.GoodColor)} prefabs...", true);

                foreach (string item in foundPrefabs)
                    global::Console.instance.Print(item);
            }
        }

        [Command("listskills", "List every available skill in the game.")]
        public void ListSkills()
        {
            var enumList = Enum.GetValues(typeof(Skills.SkillType));
            int count = enumList.Length;

            Logger.Log($"Found {count.ToString().WithColor(Logger.GoodColor)} available skills...", true);

            foreach (Skills.SkillType skillType in enumList)
                global::Console.instance.Print(skillType.ToString());
        }

        [Command("nostam", "Toggles infinite stamina.")]
        public void ToggleStamina()
        {
            if (!Plugin.NoStamina)
            {
                Player.m_localPlayer.m_staminaRegenDelay = 0.05f;
                Player.m_localPlayer.m_staminaRegen = 999f;
                Player.m_localPlayer.m_runStaminaDrain = 0f;
                Player.m_localPlayer.SetMaxStamina(9999f, true);
                Plugin.NoStamina = true;
            }
            else
            {
                Player.m_localPlayer.m_staminaRegenDelay = 1f;
                Player.m_localPlayer.m_staminaRegen = 5f;
                Player.m_localPlayer.m_runStaminaDrain = 10f;
                Player.m_localPlayer.SetMaxStamina(100f, true);
                Plugin.NoStamina = false;
            }

            Logger.Log(
                $"Infinite stamina: {(Plugin.NoStamina ? "ON".WithColor(Logger.GoodColor) : "OFF".WithColor(Logger.ErrorColor))}",
                true
            );
        }

        [Command("nores", "Toggle building restrictions. Allows you to place objects even when the preview is red.")]
        public void ToggleBuildAnywhere()
        {
            Plugin.BuildAnywhere = !Plugin.BuildAnywhere;
            Logger.Log($"No build restrictions: {(Plugin.BuildAnywhere ? "ON".WithColor(Logger.GoodColor) : "OFF".WithColor(Logger.ErrorColor))}", true);
        }

        [Command("nosup", "Toggle the need for structural support.")]
        public void ToggleNoSupport()
        {
            Plugin.NoStructuralSupport = !Plugin.NoStructuralSupport;
            Logger.Log($"No structural support: {(Plugin.NoStructuralSupport ? "ON".WithColor(Logger.GoodColor) : "OFF".WithColor(Logger.ErrorColor))}", true);

        }

        [Command("removedrops", "Clears all item drops in a radius (meters).")]
        public void RemoveDrops(float radius = 100f)
        {
            ItemDrop[] items = GameObject.FindObjectsOfType<ItemDrop>();

            int count = 0;
            foreach (ItemDrop item in items)
            {
                ZNetView component = item.GetComponent<ZNetView>();

                if (component && Vector3.Distance(item.gameObject.transform.position, Player.m_localPlayer.gameObject.transform.position) <= radius)
                {
                    component.Destroy();
                    ++count;
                }
            }

            Logger.Log($"Removed {count.ToString().WithColor(Logger.GoodColor)} items within {radius.ToString().WithColor(Logger.GoodColor)} meters.", true);
        }

        [Command("repair", "Repairs every item in your inventory.")]
        public void Repair()
        {
            List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>();
            Player.m_localPlayer.GetInventory().GetWornItems(items);

            foreach (var data in items)
            {
                data.m_durability = data.GetMaxDurability();
            }

            Logger.Log("All your items have been repaired.", true);
        }

        [Command("setskill", "Set the level of one of your skills.")]
        public void SetSkill(string skillName, int level)
        {
            string targetSkill;

            try
            {
                targetSkill = Util.GetPartialMatch(
                    ((IEnumerable<Skills.SkillType>)Enum.GetValues(typeof(Skills.SkillType))).Select(skill => skill.ToString()),
                    skillName
                );
            }
            catch (TooManyValuesException)
            {
                Logger.Error($"Found more than one skill containing the text {skillName.WithColor(Color.white)}, please be more specific.", true);
                return;
            }
            catch (NoMatchFoundException)
            {
                Logger.Error($"Couldn't find a skill named {skillName.WithColor(Color.white)}.", true);
                return;
            }

            level = Mathf.Clamp(level, 0, 100);

            Player.m_localPlayer.GetSkills().CheatResetSkill(targetSkill);
            Player.m_localPlayer.GetSkills().CheatRaiseSkill(targetSkill, level);

            Logger.Log($"Set {targetSkill.WithColor(Logger.GoodColor)} to level {level.ToString().WithColor(Logger.GoodColor)}.", true);
        }

        [Command("spawn", "Spawn a prefab/creature/item. If it's a creature, levelOrQuantity will set the level, or if it's an item, set the stack size.", nameof(GetPrefabNames))]
        public void SpawnPrefab(string prefab, int levelOrQuantity = 1)
        {
            if (!ZNetScene.instance)
            {
                Logger.Error("No scene found. Try loading a world.", true);
                return;
            }

            var netViews = UnityEngine.Object.FindObjectsOfType<ZNetView>();

            if (netViews.Length == 0)
            {
                Logger.Error("Can't locate a base ZDO to use for network reference.");
                return;
            }

            ZDO zdoManager = netViews[0].GetZDO();

            if (string.IsNullOrEmpty(prefab))
            {
                Logger.Error("You must specify a name.", true);
                return;
            }

            if (levelOrQuantity <= 0)
            {
                Logger.Error("Level/quantity to spawn must be greater than 0.", true);
                return;
            }

            GameObject prefabObject;

            try
            {
                prefabObject = Util.GetPrefabByName(prefab);
            }
            catch (TooManyValuesException)
            {
                Logger.Error($"Found more than one prefab containing the text <color=white>{prefab}</color>, please be more specific.", true);
                return;
            }
            catch (NoMatchFoundException)
            {
                Logger.Error($"Couldn't find any prefabs named <color=white>{prefab}</color>.", true);
                return;
            }

            GameObject spawned = UnityEngine.Object.Instantiate(
                prefabObject,
                Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 1.5f,
                Quaternion.identity
            );

            if (!spawned)
            {
                Logger.Error("Something went wrong!", true);
                return;
            }

            ZNetView netView = spawned.GetComponent<ZNetView>();

            Character character = spawned.GetComponent<Character>();
            ItemDrop item = spawned.GetComponent<ItemDrop>();

            if (character)
            {
                character.SetLevel(Math.Min(levelOrQuantity, 10));
            }
            else if (item && item.m_itemData != null)
            {
                item.m_itemData.m_quality = 1;
                item.m_itemData.m_durability = item.m_itemData.GetMaxDurability();
                item.m_itemData.m_stack = levelOrQuantity;
            }

            // I don't know if this is necessary, the base game's spawn command doesn't do it.
            // SkToolbox does it, seems like it's for networking reasons. I'll leave it here until I understand
            // whether it's necessary or not.
            netView.GetZDO().SetPGWVersion(zdoManager.GetPGWVersion());
            zdoManager.Set("spawn_id", netView.GetZDO().m_uid);
            zdoManager.Set("alive_time", ZNet.instance.GetTime().Ticks);

            Logger.Log($"Spawned {prefabObject.name.WithColor(Logger.GoodColor)}.", true);
        }

        [Command("time", "Overrides the time of day for you only (0 to 1 where 0.5 is noon). Set to a negative number to disable.")]
        public void SetTime(float time)
        {
            if (!EnvMan.instance)
            {
                Logger.Error("No scene found. Try loading a world.", true);
                return;
            }

            if (time >= 0f)
            {
                time = Mathf.Clamp01(time);
                EnvMan.instance.m_debugTimeOfDay = true;
                EnvMan.instance.m_debugTime = time;

                float realTime = time * 24f;
                int hour = (int)realTime;
                int minutes = (int)((realTime - (int)realTime) * 60f);
                string formatted = $"{hour.ToString().PadLeft(2, '0')}:{minutes.ToString().PadLeft(2, '0')}".WithColor(Logger.GoodColor);
                Logger.Log($"Time set to {formatted}.", true);
            }
            else
            {
                EnvMan.instance.m_debugTimeOfDay = false;
                Logger.Log("Time re-synchronized with the game.", true);
            }
        }

        [Command("unbind", "Removes a custom keybind.")]
        public void Unbind(string keyCode)
        {
            if (!Enum.TryParse(keyCode, true, out KeyCode result))
            {
                Logger.Error($"Couldn't find a key code named {keyCode.WithColor(Color.white)}.", true);
                return;
            }

            if (!Plugin.Binds.ContainsKey(result))
            {
                Logger.Error($"{result.ToString().WithColor(Color.white)} is not bound to anything.", true);
                return;
            }

            Plugin.Binds.Remove(result);
            Plugin.SaveBinds();

            Logger.Log($"Unbound {result.ToString().WithColor(Logger.GoodColor)}.", true);
        }

        [Command("unbindall", "Unbinds ALL of your Gungnir-related keybinds. Requires a true/1/yes as parameter to confirm you mean it.")]
        public void UnbindAll(bool confirm)
        {
            if (!confirm)
            {
                Logger.Error("Your binds will continue to live...", true);
                return;
            }

            Plugin.Binds.Clear();
            Plugin.SaveBinds();

            Logger.Log("All of your binds have been cleared.", true);
        }

        [Command("weather", "Overrides the weather for you only. Use -1 to clear the override.")]
        public void SetWeather(string weatherType)
        {
            if (string.IsNullOrEmpty(weatherType))
            {
                Logger.Error("You must specify a weather type.", true);
                return;
            }

            if (int.TryParse(weatherType, out int result))
            {
                if (result < 0)
                {
                    EnvMan.instance.m_debugEnv = string.Empty;
                    Logger.Log("Weather re-synchronized with the game.", true);
                    return;
                }

                Logger.Log("If you want to clear the forced weather, send a negative number next time.", true);
                return;
            }

            string targetWeather = null;

            try
            {
                targetWeather = Util.GetPartialMatch(EnvMan.instance.m_environments.Select(e => e.m_name), weatherType);
            }
            catch (NoMatchFoundException)
            {
                Logger.Error($"Couldn't find the weather type {weatherType.WithColor(Color.white)}.", true);
                return;
            }
            catch (TooManyValuesException)
            {
                Logger.Error($"Found more than one weather type containing the text {weatherType.WithColor(Color.white)}, please be more specific.", true);
                return;
            }

            EnvMan.instance.m_debugEnv = targetWeather;
            Logger.Log($"Set weather type to {targetWeather.WithColor(Logger.GoodColor)}.", true);
        }

        // Please do not edit below this line unless you really need to.
        // Thanks.

        public CommandHandler() : base()
        {
            Register();
        }

        /// <summary>
        /// Helper method to create a <see cref="CommandMeta.AutoCompleteDelegate"/> function for autocomplete handlers.
        /// </summary>
        /// <param name="method">Name of the method attached to the <see cref="CommandHandler"/> class that will provide autocomplete values.</param>
        /// <returns>A <see cref="CommandMeta.AutoCompleteDelegate"/> if the <paramref name="method"/> exists, otherwise <see langword="null"/>.</returns>
        private static CommandMeta.AutoCompleteDelegate MakeAutoCompleteDelegate(string method)
        {
            if (string.IsNullOrEmpty(method))
                return null;

            var target = typeof(CommandHandler).GetMethod(method, s_bindingFlags);

            if (target != null)
                return target.CreateDelegate(typeof(CommandMeta.AutoCompleteDelegate)) as CommandMeta.AutoCompleteDelegate;

            return null;
        }

        /// <summary>
        /// Uses reflection to find all of the methods in this class annotated with the <see cref="Command"/> attribute
        /// and registers them for execution.
        /// </summary>
        private void Register()
        {
            IEnumerable<CommandMeta> query =
                from method in typeof(CommandHandler).GetMethods()
                from attribute in method.GetCustomAttributes().OfType<Command>()
                select new CommandMeta(
                    attribute,
                    method,
                    method.GetParameters().ToList(),
                    MakeAutoCompleteDelegate(attribute.autoCompleteTarget)
                );

            Logger.Log($"Registering {query.Count()} commands...");

            // Iterate over commands in alphabetical order, so they're sorted nicely by the default help command.
            foreach (CommandMeta command in query.OrderBy(m => m.data.keyword))
            {
                /// TODO: Register with a custom prefix, maybe controlled by config?
                /// Have to reload/reregister commands when prefix is changed I guess.
                m_actions.Add("/" + command.data.keyword, command);

                string helpText;

                if (command.arguments.Count > 0)
                    helpText = $"{command.hint} - {command.data.description}";
                else
                    helpText = command.data.description;

                Terminal.ConsoleOptionsFetcher fetcher = null;

                if (command.AutoComplete != null)
                    fetcher = new Terminal.ConsoleOptionsFetcher(command.AutoComplete);

                new Terminal.ConsoleCommand("/" + command.data.keyword, helpText, delegate (Terminal.ConsoleEventArgs args)
                {
                    ParseAndRun(args.FullLine);
                }, optionsFetcher: fetcher);
                Logger.Log($"Registered command {command.data.keyword}");
            }

            Logger.Log("Finished registering commands!");
        }

        /// <summary>
        /// Attempts to run a command string that looks similar to <c>/cmd arg1 arg2 "arg with spaces"</c>.
        /// Will split arguments by spaces and quotation marks, attempt to convert each argument to the command's
        /// parameters, then run the command.
        /// </summary>
        /// <param name="text">Command string to evaluate.</param>
        public void ParseAndRun(string text)
        {
            // Remove garbage.
            text = text.Simplified();

            // Split the text using our pattern. Splits by spaces but preserves quote groups.
            List<string> args = Util.SplitByQuotes(text);

            // Store command ID and remove it from our arguments list.
            string commandName = args[0];
            args.RemoveAt(0);

            // Look up the command value, fail if it doesn't exist.
            if (!m_actions.TryGetValue(commandName, out CommandMeta command))
            {
                Logger.Error($"Unknown command <color=white>{commandName}</color>", true);
                return;
            }

            if (args.Count < command.requiredArguments)
            {
                Logger.Error($"Missing required number of arguments for <color=white>{commandName}</color>", true);
                return;
            }

            List<object> convertedArgs = new List<object>();

            // Loop through each argument type of the command object
            // and attempt to convert the corresponding text value to that type.
            // We'll unpack the converted args list into the function call which will automatically
            // cast from object -> the parameter type.
            for (int i = 0; i < command.arguments.Count; ++i)
            {
                // If there is a user supplied value, try to convert it.
                if (i < args.Count)
                {
                    Type argType = command.arguments[i].ParameterType;
                    
                    string arg = args[i];

                    object converted = null;

                    try
                    {
                        if (command.arguments[i].GetCustomAttribute(typeof(ParamArrayAttribute)) != null)
                        {
                            argType = argType.GetElementType();
                            converted = Util.StringsToObjects(args.Skip(i).ToArray(), argType);
                        }
                        else
                        {
                            converted = Util.StringToObject(arg, argType);
                        }
                    }
                    catch (SystemException e)
                    {
                        Logger.Error($"System error while converting <color=white>{arg}</color> to <color=white>{argType.Name}</color>: {e.Message}");
                    }
                    catch (TooManyValuesException)
                    {
                        Logger.Error($"Found more than one {Util.GetSimpleTypeName(argType)} with the text <color=white>{arg}</color>.", true);
                    }
                    catch (NoMatchFoundException)
                    {
                        Logger.Error($"Couldn't find a {Util.GetSimpleTypeName(argType)} with the text <color=white>{arg}</color>.", true);
                    }

                    // Couldn't convert, oh well!
                    if (converted == null)
                    {
                        Logger.Error($"Error while converting arguments for command <color=white>{commandName}</color>.", true);
                        return;
                    }

                    if (converted.GetType().IsArray)
                    {
                        object[] arr = converted as object[];
                        var things = Array.CreateInstance(argType, arr.Length);
                        Array.Copy(arr, things, arr.Length);
                        convertedArgs.Add(things);
                    }
                    else
                        convertedArgs.Add(converted);
                }
                // Otherwise, if we're still iterating, there's parameters they left unfilled.
                // This will only execute if they are optional parameters, due to our required arg count check earlier.
                else
                {
                    // Since Invoke requires all parameters to be filled, we have to manually insert the function's default value.
                    // Very silly.
                    convertedArgs.Add(command.arguments[i].DefaultValue);
                }
            }

            Logger.Log("Running command " + command.data.keyword);
            // Invoke the method, which will expand all the arguments automagically.
            command.method.Invoke(this, convertedArgs.ToArray());
        }
    }
}
