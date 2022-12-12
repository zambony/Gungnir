using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
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
        private Dictionary<string, string>      m_aliases = new Dictionary<string, string>();

        private CustomConsole m_console = null;
        private Gungnir       m_plugin  = null;

        private const BindingFlags s_bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        internal CustomConsole Console { get => m_console; set => m_console = value; }
        internal Gungnir Plugin { get => m_plugin; set => m_plugin = value; }
        public Dictionary<string, string> Aliases { get => m_aliases; set => m_aliases = value; }

        /// <summary>
        /// Helper function for the give command's autocomplete feature.
        /// </summary>
        /// <returns>A <see cref="List{string}"/> of all prefab names if the Scene is loaded, otherwise <see langword="null"/>.</returns>
        private static List<string> GetPrefabNames()
        {
            return ZNetScene.instance?.GetPrefabNames();
        }

        [Command("alias", "Create a shortcut or alternate name for a command, or sequence of commands.")]
        public void Alias(string name, params string[] commandText)
        {
            if (name.Contains(" "))
            {
                Logger.Error("An alias cannot contain spaces.", true);
                return;
            }

            if (m_actions.ContainsKey(name))
            {
                Logger.Error($"{name.WithColor(Color.white)} is a Gungnir command and cannot be overwritten.", true);
                return;
            }
            else if (Util.GetPrivateStaticField<Dictionary<string, Terminal.ConsoleCommand>>(typeof(Terminal), "commands").ContainsKey(name))
            {
                Logger.Error($"{name.WithColor(Color.white)} is a built-in Valheim command and cannot be overwritten.", true);
                return;
            }

            if (m_aliases.ContainsKey(name))
                m_aliases.Remove(name);

            string cmd = string.Join(" ", commandText);
            m_aliases.Add(name, cmd);
            Plugin.SaveAliases();

            Logger.Log($"Alias {name.WithColor(Logger.WarningColor)} created for {cmd.WithColor(Logger.WarningColor)}", true);
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

        [Command("butcher", "Kills all living creatures within a radius (meters), excluding players.")]
        public void KillAll(float radius = 50f, bool killTamed = false)
        {
            if (radius <= 0f)
            {
                Logger.Error($"Radius must be greater than 0.", true);
                return;
            }

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

            Logger.Log($"Murdered {count.ToString().WithColor(Logger.GoodColor)} creature(s) within {radius.ToString().WithColor(Logger.GoodColor)} meters.", true);
        }

        [Command("clear", "Clears the console's output.")]
        public void ClearConsole()
        {
            Console?.ClearScreen();
        }

        [Command("creative", "Toggles creative mode, which removes the need for resources.")]
        public void ToggleNoCost(bool? enabled = null)
        {
            enabled = enabled ?? !Player.m_localPlayer.GetPrivateField<bool>("m_noPlacementCost");

            Player.m_localPlayer.SetPrivateField("m_noPlacementCost", enabled);

            if (!(bool)enabled && !Player.m_localPlayer.IsDebugFlying())
                Player.m_debugMode = false;
            else
                Player.m_debugMode = true;

            Player.m_localPlayer.InvokePrivate<object>("UpdateAvailablePiecesList");

            Logger.Log($"Creative mode: {((bool)enabled ? "ON".WithColor(Logger.GoodColor) : "OFF".WithColor(Logger.ErrorColor))}", true);
        }

        [Command("echo", "Shout into the void.")]
        public void Echo(params string[] values)
        {
            global::Console.instance.Print(string.Join(" ", values));
        }

        [Command("fly", "Toggles the ability to fly. Can also disable collisions with the second argument.")]
        public void ToggleFly(bool noCollision = false)
        {
            bool enabled = Player.m_localPlayer.ToggleDebugFly();

            if (enabled)
                Player.m_localPlayer.GetComponent<Collider>().enabled = !noCollision;
            else
                Player.m_localPlayer.GetComponent<Collider>().enabled = true;

            if (!enabled && !Player.m_localPlayer.NoCostCheat())
                Player.m_debugMode = false;
            else
                Player.m_debugMode = true;

            Logger.Log(
                $"Flight: {(enabled ? "ON".WithColor(Logger.GoodColor) : "OFF".WithColor(Logger.ErrorColor))} | No collision: {(noCollision && enabled ? "ON".WithColor(Logger.GoodColor) : "OFF".WithColor(Logger.ErrorColor))}", true);
        }

        [Command("give", "Give an item to yourself or another player.", nameof(GetPrefabNames))]
        public void Give(string itemName, int amount = 1, Player player = null, int level = 1)
        {
            if (!ZNetScene.instance)
            {
                Logger.Error("No world loaded.", true);
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
                Logger.Error($"Found more than one item containing the text <color=white>{itemName}</color>, please be more specific.", true);
                return;
            }
            catch (NoMatchFoundException)
            {
                Logger.Error($"Couldn't find any items named <color=white>{itemName}</color>.", true);
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

            /// TODO: Seems like some custom RPC handlers are needed to actually insert something into
            /// someone else's inventory. https://github.com/zambony/Gungnir/issues/14
            /// Just let it drop on the ground near them if it's not the local player for now.
            if (player == Player.m_localPlayer)
                player.Pickup(spawned, autoequip: false, autoPickupDelay: false);
        }

        [Command("goto", "Teleport yourself to another player.")]
        public void Goto(Player player)
        {
            if (Player.m_localPlayer == null)
            {
                Logger.Error("No world loaded.", true);
                return;
            }

            Player.m_localPlayer.transform.position = player.transform.position - (player.transform.forward * 1.5f) + (Vector3.up * 2f);

            Logger.Log($"Teleporting to {player.GetPlayerName().WithColor(Logger.GoodColor)}...");
        }

        [Command("ghost", "Toggles ghost mode. Prevents hostile creatures from detecting you.")]
        public void ToggleGhostMode(bool? enabled = null)
        {
            enabled = enabled ?? !Player.m_localPlayer.InGhostMode();

            Player.m_localPlayer.SetGhostMode((bool)enabled);
            Logger.Log($"Ghost mode: {(Player.m_localPlayer.InGhostMode() ? "ON".WithColor(Logger.GoodColor) : "OFF".WithColor(Logger.ErrorColor))}", true);
        }

        [Command("god", "Toggles invincibility.")]
        public void ToggleGodmode(bool? enabled = null)
        {
            enabled = enabled ?? !Player.m_localPlayer.InGodMode();

            Player.m_localPlayer.SetGodMode((bool)enabled);
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

        [Command("listaliases", "List all of your custom aliases, or check what a specific alias does.")]
        public void ListAliases(string alias = null)
        {
            if (m_aliases.Count == 0)
            {
                Logger.Error($"You have no aliases currently set. Use {"/alias".WithColor(Color.white)} to add some.", true);
                return;
            }

            if (string.IsNullOrEmpty(alias))
            {
                foreach (var pair in m_aliases)
                    global::Console.instance.Print($"{pair.Key} = {pair.Value.WithColor(Logger.WarningColor)}");

                return;
            }

            if (!m_aliases.TryGetValue(alias, out string cmd))
            {
                Logger.Error($"The alias {alias.WithColor(Color.white)} does not exist.", true);
                return;
            }

            global::Console.instance.Print($"{alias} = {cmd.WithColor(Logger.WarningColor)}");
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
                foreach (var pair in Plugin.Binds)
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
                Logger.Error("No world loaded.", true);
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
                Logger.Error("No world loaded.", true);
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

        [Command("listportals", "List every portal tag.")]
        public void ListPortals()
        {
            HashSet<string> tagSet = new HashSet<string>();

            int portalHash = Game.instance.m_portalPrefab.name.GetStableHashCode();

            var query =
                from pair in ZDOMan.instance.GetPrivateField<Dictionary<ZDOID, ZDO>>("m_objectsByID")
                let tag = pair.Value.GetString("tag", null)
                where pair.Value.GetPrefab() == portalHash && !string.IsNullOrEmpty(tag) && tag != " "
                select pair.Value;

            foreach (ZDO zdo in query)
            {
                string tag = zdo.GetString("tag", null);

                if (!tagSet.Contains(tag))
                    tagSet.Add(tag);
            }

            Logger.Log($"Found {tagSet.Count.ToString().WithColor(Logger.GoodColor)} tag(s)...", true);

            foreach (string tag in tagSet.OrderBy(tag => tag))
                global::Console.instance.Print(tag);
        }

        [Command("listprefabs", "List every prefab in the game, or search for one that contains your text.")]
        public void ListPrefabs(string prefabName = null)
        {
            if (!ZNetScene.instance)
            {
                Logger.Error("No world loaded.", true);
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

        [Command("nomana", "Toggles infintie eitr (mana).")]
        public void ToggleMana(bool? enabled = null)
        {
            enabled = enabled ?? !Plugin.NoMana;

            if ((bool)enabled)
            {
                Player.m_localPlayer.m_eitrRegenDelay = 0.0f;
                Player.m_localPlayer.m_eiterRegen = 1000f;
                Player.m_localPlayer.SetMaxEitr(1000f, true);
                Plugin.NoMana = true;
            }
            else
            {
                Player.m_localPlayer.m_eitrRegenDelay = 1f;
                Player.m_localPlayer.m_eiterRegen = 5f;
                Player.m_localPlayer.SetMaxEitr(100f, true);
                Plugin.NoMana = false;
            }

            Logger.Log(
                $"Infinite mana: {(Plugin.NoMana ? "ON".WithColor(Logger.GoodColor) : "OFF".WithColor(Logger.ErrorColor))}",
                true
            );
        }

        [Command("nostam", "Toggles infinite stamina.")]
        public void ToggleStamina(bool? enabled = null)
        {
            enabled = enabled ?? !Plugin.NoStamina;

            if ((bool)enabled)
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
        public void ToggleBuildAnywhere(bool? enabled = null)
        {
            enabled = enabled ?? !Plugin.BuildAnywhere;

            Plugin.BuildAnywhere = (bool)enabled;
            Logger.Log($"No build restrictions: {(Plugin.BuildAnywhere ? "ON".WithColor(Logger.GoodColor) : "OFF".WithColor(Logger.ErrorColor))}", true);
        }

        [Command("noslide", "Toggle the ability to walk up steep angles without sliding.")]
        public void ToggleNoSlide(bool? enabled = null)
        {
            enabled = enabled ?? !Plugin.NoSlide;

            Plugin.NoSlide = (bool)enabled;
            Logger.Log($"No slide: {(Plugin.NoSlide ? "ON".WithColor(Logger.GoodColor) : "OFF".WithColor(Logger.ErrorColor))}", true);
        }

        [Command("nosup", "Toggle the need for structural support.")]
        public void ToggleNoSupport(bool? enabled = null)
        {
            enabled = enabled ?? !Plugin.NoSlide;

            Plugin.NoStructuralSupport = (bool)enabled;
            Logger.Log($"No structural support: {(Plugin.NoStructuralSupport ? "ON".WithColor(Logger.GoodColor) : "OFF".WithColor(Logger.ErrorColor))}", true);
        }

        [Command("pos", "Print your current position as XZY coordinates. (XZY is used for tp command.)")]
        public void Pos()
        {
            Vector3 pos = Player.m_localPlayer.transform.position;
            string fmt = $"{Math.Round(pos.x, 3)} {Math.Round(pos.z, 3)} {Math.Round(pos.y, 3)}".WithColor(Logger.GoodColor);
            Logger.Log($"Your current position is {fmt}", true);
        }

        [Command("puke", "Clears all food buffs and makes room for you to eat something else.")]
        public void Puke()
        {
            Player.m_localPlayer.ClearFood();
            Logger.Log("Food buffs cleared.", true);
        }

        [Command("removedrops", "Clears all item drops in a radius (meters).")]
        public void RemoveDrops(float radius = 50f)
        {
            if (radius <= 0f)
            {
                Logger.Error($"Radius must be greater than 0.", true);
                return;
            }

            ItemDrop[] items = GameObject.FindObjectsOfType<ItemDrop>();

            int count = 0;
            foreach (ItemDrop item in items)
            {
                ZNetView component = item.GetComponent<ZNetView>();

                if (component && Vector3.Distance(item.gameObject.transform.position, Player.m_localPlayer.gameObject.transform.position) <= radius)
                {
                    component.ClaimOwnership();
                    component.Destroy();
                    ++count;
                }
            }

            Logger.Log($"Removed {count.ToString().WithColor(Logger.GoodColor)} item(s) within {radius.ToString().WithColor(Logger.GoodColor)} meters.", true);
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

        [Command("repairbuilds", "Repairs all nearby structures within a radius (meters).")]
        public void RepairBuildings(float radius = 50f)
        {
            if (Player.m_localPlayer == null)
            {
                Logger.Error("No world loaded.", true);
                return;
            }

            if (radius <= 0f)
            {
                Logger.Error($"Radius must be greater than 0.", true);
                return;
            }

            int count = 0;

            foreach (WearNTear obj in WearNTear.GetAllInstaces())
            {
                if (obj.Repair() && Vector3.Distance(obj.gameObject.transform.position, Player.m_localPlayer.transform.position) <= radius)
                    ++count;
            }

            Logger.Log($"Repaired {count.ToString().WithColor(Logger.GoodColor)} structure(s) within {radius.ToString().WithColor(Logger.GoodColor)} meters.", true);
        }

        [Command("setmaxweight", "Set your maximum carry weight.")]
        public void SetCarryWeight(float maxWeight = 300f)
        {
            if (maxWeight <= 0f)
            {
                Logger.Error("Max weight must be greater than zero.", true);
                return;
            }

            Player.m_localPlayer.m_maxCarryWeight = maxWeight;

            Logger.Log($"Set maximum carry weight to {maxWeight.ToString().WithColor(Logger.GoodColor)}.", true);
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
            Player.m_localPlayer.GetSkills().CheatRaiseSkill(targetSkill, level, false);

            Logger.Log($"Set {targetSkill.WithColor(Logger.GoodColor)} to level {level.ToString().WithColor(Logger.GoodColor)}.", true);
        }

        [Command("seed", "Print the seed used by this world.")]
        public void Seed()
        {
            if (ZNetScene.instance == null)
            {
                Logger.Error("No world loaded.", true);
                return;
            }

            Logger.Log(WorldGenerator.instance.GetPrivateField<World>("m_world").m_seedName.WithColor(Logger.GoodColor), true);
        }

        [Command("spawn", "Spawn a prefab/creature/item. If it's a creature, levelOrQuantity will set the level, or if it's an item, set the stack size.", nameof(GetPrefabNames))]
        public void SpawnPrefab(string prefab, int levelOrQuantity = 1)
        {
            if (!ZNetScene.instance)
            {
                Logger.Error("No world loaded.", true);
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

        [Command("spawntamed", "Spawn a tamed version of a creature.")]
        public void SpawnTamed(string creature, int level = 1)
        {
            if (!ZNetScene.instance)
            {
                Logger.Error("No world loaded.", true);
                return;
            }

            if (string.IsNullOrEmpty(creature))
            {
                Logger.Error("You must specify a name.", true);
                return;
            }

            if (level <= 0)
            {
                Logger.Error("Level must be greater than 0.", true);
                return;
            }

            GameObject prefabObject;

            try
            {
                prefabObject = Util.GetPrefabByName(creature);
            }
            catch (TooManyValuesException)
            {
                Logger.Error($"Found more than one creature containing the text <color=white>{creature}</color>, please be more specific.", true);
                return;
            }
            catch (NoMatchFoundException)
            {
                Logger.Error($"Couldn't find any creatures named <color=white>{creature}</color>.", true);
                return;
            }

            Character character = prefabObject.GetComponent<Character>();

            if (character == null)
            {
                Logger.Error($"{prefabObject.name.WithColor(Color.white)} is not a valid creature.", true);
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

            character = spawned.GetComponent<Character>();
            character.gameObject.GetComponent<MonsterAI>().MakeTame();
            character.SetLevel(Math.Min(level, 10));

            Logger.Log($"Spawned a tame level {level.ToString().WithColor(Logger.WarningColor)} {prefabObject.name.WithColor(Logger.GoodColor)}.", true);
        }

        [Command("tame", "Pacify all tameable creatures in a radius.")]
        public void Tame(float radius = 10f)
        {
            if (radius <= 0f)
            {
                Logger.Error($"Radius must be greater than 0.", true);
                return;
            }

            List<Character> characters = new List<Character>();
            Character.GetCharactersInRange(Player.m_localPlayer.transform.position, radius, characters);

            int count = 0;

            foreach (Character character in characters)
            {
                if (character.gameObject.GetComponent<Tameable>() != null)
                {
                    character.gameObject.GetComponent<MonsterAI>().MakeTame();
                    ++count;
                }
            }

            Logger.Log($"Tamed {count.ToString().WithColor(Logger.GoodColor)} creatures within {radius.ToString().WithColor(Logger.GoodColor)} meters.", true);
        }

        [Command("time", "Overrides the time of day for you only (0 to 1 where 0.5 is noon). Set to a negative number to disable.")]
        public void SetTime(float time)
        {
            if (!EnvMan.instance)
            {
                Logger.Error("No world loaded.", true);
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

        // Start terrain commands. These are not arranged alphabetically with the rest of the commands
        // because they are all very relevant, and it seemed appropriate to leave them grouped tightly.

        [Command("tlevel", "Level the terrain in a radius.")]
        public void TerrainLevel(float radius = 10f)
        {
            if (radius <= 0f)
            {
                Logger.Error("Radius must be greater than 0.", true);
                return;
            }
            else if (radius > 50f)
            {
                Logger.Error("To avoid crashing your or another player's game, your radius will be clamped to 50.", true);
                radius = 50f;
            }

            Ground.Level(Player.m_localPlayer.transform.position, radius);
            Logger.Log($"Terrain within {radius.ToString().WithColor(Logger.GoodColor)} meter(s) leveled.", true);
        }

        [Command("tlower", "Lower the terrain in a radius by some amount. Strength values closer to 0 make the terrain edges steep, while values further from 0 make them smoother.")]
        public void TerrainLower(float radius = 10f, float depth = 1f, float strength = 0.01f)
        {
            if (radius <= 0f)
            {
                Logger.Error("Radius must be greater than 0.", true);
                return;
            }
            else if (radius > 50f)
            {
                Logger.Error("To avoid crashing your or another player's game, your radius will be clamped to 50.", true);
                radius = 50f;
            }

            if (depth <= 0f)
            {
                Logger.Error("Depth must be greater than 0.", true);
                return;
            }

            if (strength <= 0f)
            {
                Logger.Error("Strength must be greater than 0.", true);
                return;
            }

            Ground.Lower(Player.m_localPlayer.transform.position, radius, depth, strength);
            Logger.Log($"Terrain within {radius.ToString().WithColor(Logger.GoodColor)} meter(s) lowered.", true);
        }

        [Command("tpaint", "Paint terrain in a radius. Available paint types are: dirt, paved, cultivate, and reset.")]
        public void TerrainPaint(float radius = 5f, string paintType = "reset")
        {
            if (radius <= 0f)
            {
                Logger.Error("Radius must be greater than 0.", true);
                return;
            }
            else if (radius > 50f)
            {
                Logger.Error("To avoid crashing your or another player's game, your radius will be clamped to 50.", true);
                radius = 50f;
            }

            if (!Enum.TryParse(paintType, true, out TerrainModifier.PaintType type))
            {
                Logger.Error($"No paint type named {paintType.WithColor(Color.white)}", true);
                return;
            }

            Ground.Paint(Player.m_localPlayer.transform.position, radius, type);
            Logger.Log($"Terrain within {radius.ToString().WithColor(Logger.GoodColor)} meter(s) painted.", true);
        }

        [Command("traise", "Raise the terrain in a radius by some amount. Strength values closer to 0 make the terrain edges steep, while values further from 0 make them smoother.")]
        public void TerrainRaise(float radius = 10f, float height = 1f, float strength = 0.01f)
        {
            if (radius <= 0f)
            {
                Logger.Error("Radius must be greater than 0.", true);
                return;
            }
            else if (radius > 50f)
            {
                Logger.Error("To avoid crashing your or another player's game, your radius will be clamped to 50.", true);
                radius = 50f;
            }

            if (height <= 0f)
            {
                Logger.Error("Height must be greater than 0.", true);
                return;
            }

            if (strength <= 0f)
            {
                Logger.Error("Strength must be greater than 0.", true);
                return;
            }

            Ground.Raise(Player.m_localPlayer.transform.position, radius, height, strength);
            Logger.Log($"Terrain within {radius.ToString().WithColor(Logger.GoodColor)} meter(s) raised.", true);
        }

        [Command("treset", "Reset all terrain modifications in a radius.")]
        public void TerrainReset(float radius = 10f)
        {
            if (radius <= 0f)
            {
                Logger.Error("Radius must be greater than 0.", true);
                return;
            }
            else if (radius > 50f)
            {
                Logger.Error("To avoid crashing your or another player's game, your radius will be clamped to 50.", true);
                radius = 50f;
            }

            Ground.Reset(Player.m_localPlayer.transform.position, radius);
            Logger.Log($"Terrain within {radius.ToString().WithColor(Logger.GoodColor)} meter(s) reset.", true);
        }

        [Command("tshape", "Choose the shape of terrain modifications with 'circle' or 'square'.")]
        public void TerrainShape(string shape)
        {
            if (shape.Equals("square", StringComparison.OrdinalIgnoreCase) ||
                shape.Equals("box", StringComparison.OrdinalIgnoreCase) ||
                shape.Equals("cube", StringComparison.OrdinalIgnoreCase))
                Ground.square = true;
            else if (shape.Equals("circle", StringComparison.OrdinalIgnoreCase) ||
                shape.Equals("sphere", StringComparison.OrdinalIgnoreCase) ||
                shape.Equals("round", StringComparison.OrdinalIgnoreCase))
                Ground.square = false;
            else
            {
                Logger.Error($"Only {"square".WithColor(Color.white)} and {"circle".WithColor(Color.white)} are acceptable values.", true);
                return;
            }

            Logger.Log($"Terrain modification shape: {(Ground.square ? "square".WithColor(Logger.GoodColor) : "circle".WithColor(Logger.GoodColor))}", true);
        }

        [Command("tsmooth", "Smooth terrain in a radius with some strength. Higher strengths mean more aggressive smoothing.")]
        public void TerrainSmooth(float radius = 10f, float strength = 0.5f)
        {
            if (radius <= 0f)
            {
                Logger.Error("Radius must be greater than 0.", true);
                return;
            }
            else if (radius > 50f)
            {
                Logger.Error("To avoid crashing your or another player's game, your radius will be clamped to 50.", true);
                radius = 50f;
            }

            if (strength <= 0f)
            {
                Logger.Error("Strength must be greater than 0.", true);
                return;
            }

            Ground.Smooth(Player.m_localPlayer.transform.position, radius, strength);
            Logger.Log($"Terrain within {radius.ToString().WithColor(Logger.GoodColor)} meter(s) smoothed.", true);
        }

        // End terrain commands.

        [Command("tp", "Teleport to specific coordinates. The Y value is optional. If omitted, will attempt to find the best height to put you at automagically.")]
        public void Teleport(float x, float z, float? y = null)
        {
            if (!Player.m_localPlayer)
            {
                Logger.Error("No world loaded.", true);
                return;
            }

            if (y == null)
            {
                Physics.Raycast(new Vector3(x, 5000, z), Vector3.down, out RaycastHit hit, 10000);
                y = hit.point.y;
            }

            Player.m_localPlayer.transform.position = new Vector3(x, (float)y, z);
            Logger.Log("Woosh!", true);
        }

        [Command("unalias", "Remove an alias you've created.")]
        public void Unalias(string alias)
        {
            if (m_aliases.ContainsKey(alias))
            {
                m_aliases.Remove(alias);
                Logger.Log($"Alias {alias.WithColor(Logger.GoodColor)} deleted.", true);

                return;
            }

            Logger.Error($"No alias named {alias.WithColor(Color.white)} exists.", true);
        }

        [Command("unaliasall", "Removes all of your custom command aliases. Requires a true/1/yes as parameter to confirm you mean it.")]
        public void UnaliasAll(bool confirm)
        {
            if (!confirm)
            {
                Logger.Error("Your aliases are safe.", true);
                return;
            }

            m_aliases.Clear();
            Plugin.SaveAliases();

            Logger.Log("All of your aliases have been cleared.", true);
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
                Logger.Error("Your binds are safe.", true);
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
                    Run(args.FullLine);
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
        public void Run(string text)
        {
            // Remove garbage.
            text = text.Simplified();

            // Try to convert any aliases to their contents.
            text = ReplaceAlias(text);

            // Split the text using our pattern. Splits by spaces but preserves quote groups.
            List<string> args = Util.SplitByQuotes(text);

            // Store command ID and remove it from our arguments list.
            string commandName = args[0];
            args.RemoveAt(0);

            // Look up the command value, fail if it doesn't exist.
            if (!m_actions.TryGetValue(commandName, out CommandMeta command))
            {
                Logger.Error($"Unknown command <color=white>{commandName}</color>.", true);
                return;
            }

            if (args.Count < command.requiredArguments)
            {
                Logger.Error($"Missing required number of arguments for <color=white>{commandName}</color>.", true);
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
            try
            {
                command.method.Invoke(this, convertedArgs.ToArray());
            }
            catch (Exception)
            {
                Logger.Error($"Something happened while running {command.data.keyword.WithColor(Color.white)}, check the BepInEx console for more details.", true);
                throw;
            }
        }

        public CommandMeta GetCommand(string commandName)
        {
            if (!commandName.StartsWith("/"))
                commandName = "/" + commandName;

            if (!m_actions.TryGetValue(commandName, out CommandMeta command))
                return null;

            return command;
        }

        public string GetAlias(string input)
        {
            if (m_aliases.TryGetValue(input, out string value))
                return value;

            return null;
        }

        public string ReplaceAlias(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string[] split = input.Split();

            string alias = GetAlias(split[0]);

            if (!string.IsNullOrEmpty(alias))
            {
                split[0] = alias;

                return string.Join(" ", split);
            }

            return input;
        }
    }

    internal static class Ground
    {
        internal enum Operation
        {
            None,
            Level,
            Raise,
            Lower,
            Smooth,
            Paint
        }

        public static bool square = false;

        public static void Level(Vector3 position, float radius)
        {
            GameObject prefab = Util.GetHiddenPrefab("mud_road_v2");

            Make(prefab, position, radius, Operation.Level);
        }

        public static void Raise(Vector3 position, float radius, float height, float strength = 0.01f)
        {
            GameObject prefab = Util.GetHiddenPrefab("raise_v2");

            Make(prefab, position, radius, Operation.Raise, height, strength);
        }

        public static void Lower(Vector3 position, float radius, float depth, float strength = 0.01f)
        {
            GameObject prefab = Util.GetHiddenPrefab("digg_v3");

            Make(prefab, position, radius, Operation.Lower, depth, strength);
        }

        public static void Smooth(Vector3 position, float radius, float strength = 0.5f)
        {
            GameObject prefab = Util.GetHiddenPrefab("mud_road_v2");

            Make(prefab, position, radius, Operation.Smooth, strength);
        }

        public static void Paint(Vector3 position, float radius, TerrainModifier.PaintType type)
        {
            GameObject prefab = null;

            if (type == TerrainModifier.PaintType.Dirt)
                prefab = Util.GetHiddenPrefab("mud_road_v2");
            else if (type == TerrainModifier.PaintType.Paved)
                prefab = Util.GetHiddenPrefab("paved_road_v2");
            else if (type == TerrainModifier.PaintType.Reset)
                prefab = Util.GetHiddenPrefab("replant_v2");
            else if (type == TerrainModifier.PaintType.Cultivate)
                prefab = Util.GetHiddenPrefab("cultivate_v2");
            else
                return;

            Make(prefab, position, radius, Operation.Paint);
        }

        public static void Reset(Vector3 position, float radius)
        {
            // Remove all Terrain V2 edits. These are typically done by mods,
            // or are present in very old save files that haven't run the "optterrain" console command.
            foreach (var obj in TerrainModifier.GetAllInstances())
            {
                if (Utils.DistanceXZ(position, obj.transform.position) <= radius)
                {
                    ZNetView netView = obj.GetComponent<ZNetView>();

                    if (netView == null)
                        continue;

                    netView.ClaimOwnership();
                    netView.Destroy();
                }
            }

            List<Heightmap> heightmaps = new List<Heightmap>();
            Heightmap.FindHeightmap(position, radius + 50f, heightmaps);

            bool resetGrass = false;

            foreach (Heightmap heightmap in heightmaps)
            {
                bool modified = false;
                TerrainComp compiler = TerrainComp.FindTerrainCompiler(heightmap.transform.position);

                if (compiler == null)
                    continue;

                if (!compiler.GetPrivateField<bool>("m_initialized"))
                    continue;

                heightmap.WorldToVertex(position, out int x, out int y);

                int     width          = compiler.GetPrivateField<int>("m_width");
                float[] levelDelta     = compiler.GetPrivateField<float[]>("m_levelDelta");
                float[] smoothDelta    = compiler.GetPrivateField<float[]>("m_smoothDelta");
                bool[]  modifiedHeight = compiler.GetPrivateField<bool[]>("m_modifiedHeight");
                Color[] paintMask      = compiler.GetPrivateField<Color[]>("m_paintMask");
                bool[]  modifiedPaint  = compiler.GetPrivateField<bool[]>("m_modifiedPaint");

                for (int h = 0; h <= width; ++h)
                {
                    for (int w = 0; w <= width; ++w)
                    {
                        if (Util.Distance2D(x, y, w, h) > radius)
                            continue;

                        int heightIndex = w + (h * (width + 1));

                        if (modifiedHeight[heightIndex])
                        {
                            modifiedHeight[heightIndex] = false;
                            levelDelta[heightIndex] = 0;
                            smoothDelta[heightIndex] = 0;
                            modified = true;
                            resetGrass = true;
                        }

                        if (h < width && w < width)
                        {
                            int paintIndex = w + (h * width);

                            if (modifiedPaint[paintIndex])
                            {
                                modifiedPaint[paintIndex] = false;
                                paintMask[paintIndex] = Color.clear;
                                modified = true;
                                resetGrass = true;
                            }
                        }
                    }
                }

                if (!modified)
                    continue;

                compiler.SetPrivateField("m_width", width);
                compiler.SetPrivateField("m_levelDelta", levelDelta);
                compiler.SetPrivateField("m_smoothDelta", smoothDelta);
                compiler.SetPrivateField("m_modifiedHeight", modifiedHeight);
                compiler.SetPrivateField("m_paintMask", paintMask);
                compiler.SetPrivateField("m_modifiedPaint", modifiedPaint);

                compiler.InvokePrivate<object>("Save");
                heightmap.Poke(true);
            }

            if (ClutterSystem.instance != null && resetGrass)
                ClutterSystem.instance.ResetGrass(position, radius);
        }

        private static void Make(GameObject prefab, Vector3 position, float radius, Operation op, params object[] args)
        {
            // This might stop some of the particle spam when making terrain mods.
            TerrainModifier.SetTriggerOnPlaced(false);
            bool wasActive = prefab.activeSelf || prefab.activeInHierarchy;
            // All the terrain prefabs we're using apply terrain modifications the moment they spawn.
            // We want to modify the operation before it's applied, so disabling the object before instantiating it
            // allows us to delay the Awake() method.
            prefab.SetActive(false);
            TerrainOp mod = prefab.GetComponentInChildren<TerrainOp>();

            // Some ground pieces spawn with an offset. Account for it.
            float levelOffset = mod.m_settings.m_levelOffset;

            GameObject spawned = UnityEngine.Object.Instantiate(prefab, position - Vector3.up * levelOffset, Quaternion.identity);
            // Restore the original prefab to its active state since we disabled it before making a clone.
            prefab.SetActive(wasActive);

            mod = spawned.GetComponentInChildren<TerrainOp>();

            // Delete all the smoke/rock/dirt particle effects on spawn.
            mod.m_onPlacedEffect = new EffectList();

            // Modify terrain settings.
            mod.m_settings.m_square = square;

            if (op == Operation.Level)
            {
                mod.m_settings.m_level = true;
                mod.m_settings.m_levelRadius = radius;
                mod.m_settings.m_levelOffset = 0f;
                mod.m_settings.m_smooth = false;
            }
            else if (op == Operation.Raise || op == Operation.Lower)
            {
                mod.m_settings.m_raise = true;
                mod.m_settings.m_raiseRadius = radius;
                mod.m_settings.m_raiseDelta = (float)args[0];
                mod.m_settings.m_raisePower = (float)args[1];

                if (op == Operation.Lower)
                    mod.m_settings.m_raiseDelta *= -1f;
            }
            else if (op == Operation.Smooth)
            {
                mod.m_settings.m_level = false;
                mod.m_settings.m_smooth = true;
                mod.m_settings.m_smoothPower = (float)args[0];
            }
            else if (op == Operation.Paint)
            {
                mod.m_settings.m_level = false;
                mod.m_settings.m_smooth = false;
                mod.m_settings.m_raise = false;
            }

            mod.m_settings.m_paintRadius = radius;
            mod.m_settings.m_smoothRadius = radius;

            // All done, let Unity call Awake() and stuff.
            spawned.SetActive(true);
        }
    }
}
