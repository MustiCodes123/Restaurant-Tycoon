using UnityEngine;

namespace com.vivuga.food
{
        public class CharacterSwitcherWithUI: MonoBehaviour
        {
                [Header("Characters Parent")]
                [SerializeField] private Transform charactersParent;

                [Header("UI Panels")]
                [SerializeField] private GameObject[] uiPanels;

                private GameObject[] characters;
                private int currentIndex = 0;

                void Start()
                {
                        if (charactersParent == null)
                                return;

                        characters = new GameObject[charactersParent.childCount];

                        for (int i = 0; i < charactersParent.childCount; i++)
                        {
                                characters[i] = charactersParent.GetChild(i).gameObject;
                        }

                        ShowCharacter(currentIndex);
                }

                public void NextCharacter()
                {
                        if (characters == null || characters.Length == 0)
                                return;

                        currentIndex++;

                        if (currentIndex >= characters.Length)
                                currentIndex = 0;

                        ShowCharacter(currentIndex);
                }

                public void PreviousCharacter()
                {
                        if (characters == null || characters.Length == 0)
                                return;

                        currentIndex--;

                        if (currentIndex < 0)
                                currentIndex = characters.Length - 1;

                        ShowCharacter(currentIndex);
                }

                private void ShowCharacter(int index)
                {
                        for (int i = 0; i < characters.Length; i++)
                        {
                                if (characters[i] != null)
                                        characters[i].SetActive(i == index);
                        }

                        for (int i = 0; i < uiPanels.Length; i++)
                        {
                                if (uiPanels[i] != null)
                                        uiPanels[i].SetActive(i == index);
                        }
                }
        }
}