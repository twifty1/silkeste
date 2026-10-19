using UnityEngine;

namespace MadelineInPharloom;

public class MirrorBlit : MonoBehaviour
{
	private static readonly Vector2 FlipScale = new Vector2(-1f, 1f);

	private static readonly Vector2 FlipOffset = new Vector2(1f, 0f);

	public static bool Active
	{
		get
		{
			GameManager silentInstance = GameManager.SilentInstance;
			if (AssistMode.MirrorMode && silentInstance != null)
			{
				return !silentInstance.IsMenuScene();
			}
			return false;
		}
	}

	private void OnRenderImage(RenderTexture src, RenderTexture dst)
	{
		if (Active)
		{
			Graphics.Blit(src, dst, FlipScale, FlipOffset);
		}
		else
		{
			Graphics.Blit(src, dst);
		}
	}
}
