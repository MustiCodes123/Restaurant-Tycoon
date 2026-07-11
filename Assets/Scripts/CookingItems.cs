using UnityEngine;

namespace RestaurantTycoon
{
    public class CookingItems : MonoBehaviour
    {
        public Animator anim;
        public GameObject startCooking;
        public void StartCooking()
        {
            startCooking.SetActive(true);
            anim.SetTrigger("Cook");
        }
        public void EndCooking()
        {
            startCooking.SetActive(false);
        }

    }
}