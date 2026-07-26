using UnityEngine;
namespace com.vivuga.food
{
        public class MultiCharacterAnimationSwitcher: MonoBehaviour
        {
                [SerializeField] private Animator[] animators;

                public void PlayTrigger(string triggerName)
                {
                        if (animators == null || animators.Length == 0)
                                return;

                        for (int i = 0; i < animators.Length; i++)
                        {
                                if (animators[i] != null && animators[i].gameObject.activeInHierarchy)
                                {
                                        animators[i].SetTrigger(triggerName);
                                        return;
                                }
                        }
                }

                public void PlayState(string stateName)
                {
                        if (animators == null || animators.Length == 0)
                                return;

                        for (int i = 0; i < animators.Length; i++)
                        {
                                if (animators[i] != null && animators[i].gameObject.activeInHierarchy)
                                {
                                        animators[i].Play(stateName);
                                        return;
                                }
                        }
                }
        }
}