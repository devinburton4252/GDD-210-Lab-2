using UnityEngine;

public class Button : MonoBehaviour
{
	public Light light;

	public void Press()
	{
		light.enabled = !light.enabled;
	}
}
