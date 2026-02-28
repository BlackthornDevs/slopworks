using UnityEngine;

[CreateAssetMenu(menuName = "Slopworks/Recipes/Recipe")]
public class RecipeSO : ScriptableObject
{
    public string recipeId;
    public string displayName;
    public RecipeIngredient[] inputs;
    public RecipeIngredient[] outputs;
    public float craftDuration = 1f;
    public string requiredMachineType;
}

[System.Serializable]
public struct RecipeIngredient
{
    public string itemId;
    public int count;
}
