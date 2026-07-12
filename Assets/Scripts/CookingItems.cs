using UnityEngine;

namespace RestaurantTycoon
{
    public class CookingItems : MonoBehaviour
    {
        public float animationDelay = 0;
        public Animator anim;
        public GameObject startCooking;
        public void StartCooking()
        {
            startCooking.SetActive(true);
            Invoke("DelayAnimation", animationDelay);
        }
        void DelayAnimation()
        {
            anim.SetTrigger("Cook");
        }
        public void EndCooking()
        {
            startCooking.SetActive(false);
        }

    }
}