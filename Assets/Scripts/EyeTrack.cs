using UnityEngine;


public class EyeTrack : MonoBehaviour
{
    private Transform player;
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

   
    void Update()
    {
        transform.LookAt(player);
    }
}
