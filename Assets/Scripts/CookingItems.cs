using UnityEngine;

namespace RestaurantTycoon
{
    public class CookingItems : MonoBehaviour
    {
        public float animationDelay = 0;
        public Animator anim;
        public GameObject startCooking;
        public GameObject smokeParticles;
        public void StartCooking()
        {
            startCooking.SetActive(true);
            Invoke("DelayAnimation", animationDelay);
            if(smokeParticles != null)
            {
                smokeParticles.SetActive(true);
            }
        }
        void DelayAnimation()
        {
            anim.SetTrigger("Cook");
        }
        public void EndCooking()
        {
            startCooking.SetActive(false);
            if (smokeParticles != null)
            {
                smokeParticles.SetActive(false);
            }
        }

    }
}