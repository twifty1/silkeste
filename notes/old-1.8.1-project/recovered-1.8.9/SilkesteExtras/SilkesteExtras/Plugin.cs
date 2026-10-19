using BepInEx;
using HarmonyLib;

namespace SilkesteExtras;

[BepInPlugin("silkeste.extras", "Silkeste Extras", "1.8.7")]
[BepInDependency("melty.madelineinpharloom", BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BaseUnityPlugin
{
	private void Awake()
	{
		new Harmony("silkeste.extras").PatchAll(typeof(Plugin).Assembly);
		base.Logger.LogInfo("Hornet's needle attack: off while Madeline is loaded");
	}
}
