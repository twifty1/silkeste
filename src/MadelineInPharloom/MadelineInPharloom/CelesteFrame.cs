using UnityEngine;

namespace MadelineInPharloom;

public class CelesteFrame
{
	public Sprite Sprite;

	public bool HasHair = true;

	public Vector2 HairOffset;

	public int HairFrame;

	public int CarryY;

	/// <summary>Frames with Madeline's hair painted into the sprite (sleeping, sitting...): just those hair pixels, white,
	/// so they can be tinted the live hair colour like the rest of her hair.</summary>
	public Sprite HairMask;
}
