using UnityEngine;

public class HintBooster : MonoBehaviour
{
    [SerializeField] float highlightDuration = 3f;
    
    public void ActiveBooster()
    {
        var group = PassengerManager.Instance.FindBestHintGroup();

        if(group == null || group.Count == 0)
        {
            return;
        }

        foreach(var p in group)
        {
            p.SetHighLight(true, highlightDuration);
        }
    }
}
