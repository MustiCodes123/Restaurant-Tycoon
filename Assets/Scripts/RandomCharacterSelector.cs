using UnityEngine;

public class RandomCharacterSelector : MonoBehaviour
{
    [SerializeField] private Animator rootAnimator;
    [SerializeField] private CharacterData[] characters;

    [System.Serializable]
    public class CharacterData
    {
        public GameObject character;
        public Avatar avatar;
    }

    private void Awake()
    {
        if (characters.Length == 0) return;

        int randomIndex = Random.Range(0, characters.Length);

        for (int i = 0; i < characters.Length; i++)
        {
            bool active = i == randomIndex;
            characters[i].character.SetActive(active);
        }

        rootAnimator.avatar = characters[randomIndex].avatar;
    }
}