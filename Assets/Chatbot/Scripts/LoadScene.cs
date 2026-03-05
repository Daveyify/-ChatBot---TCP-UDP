using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadScene : MonoBehaviour
{
    public void LoadAdditiveScene()
    {
        SceneManager.LoadScene("Chatbot_Server", LoadSceneMode.Additive);
    }
}