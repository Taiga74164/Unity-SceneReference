using Examples.Generated;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Examples
{
    public class Test : MonoBehaviour
    {
        public void OnButtonClick()
        {
            SceneManager.LoadScene((int)SceneId.Scene0);
        }
        
        public void OnButtonClick_1()
        {
            SceneManager.LoadScene((int)SceneId.Scene1);
        }
    }
}