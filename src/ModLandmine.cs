using System.Collections;
using UnityEngine;

namespace WeekdayBonuses;

public class ModLandmine : MonoBehaviour
{
	private Landmine landmine;

	public bool DelayExplosion;

	private void Awake()
	{
		landmine = gameObject.GetComponent<Landmine>();
	}

	public void TryDetonate()
	{
		if (DelayExplosion && Plugin.Config.DoubleTrapBuffLandmineDelay.Value > 0f)
		{
			StartCoroutine(ExplodeWithDelay());
			return;
		}

		DelayExplosion = false;
		landmine.Detonate();
	}

	public IEnumerator ExplodeWithDelay()
	{
		yield return new WaitForSeconds(Plugin.Config.DoubleTrapBuffLandmineDelay.Value);

		DelayExplosion = false;
		landmine.Detonate();
	}
}
