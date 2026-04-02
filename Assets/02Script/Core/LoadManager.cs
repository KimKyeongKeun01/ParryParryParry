using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadManager : MonoBehaviour
{
    private static LoadManager instance;
    public static LoadManager Instance
    {
        get { return instance; }
        private set { instance = value; }
    }

    public static string nextSceneAddress;
    public static Action OnComplete = null;
    private  bool isLoading = false;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        Application.targetFrameRate = 60;
    }

    public void LoadScene(string sceneAddress, Action action = null)
    {
        if (isLoading) return;
        isLoading = true;

        nextSceneAddress = sceneAddress;
        StartCoroutine(CoLoadScene(action));
    }

    IEnumerator CoLoadScene(Action action = null)
    {
        yield return null;

        var sceneHandle = SceneManager.LoadSceneAsync(nextSceneAddress);

        while (!sceneHandle.isDone)
        {
            yield return null;
        }

        Resources.UnloadUnusedAssets();
        GC.Collect();

        OnComplete?.Invoke();
        OnComplete = null;

        action?.Invoke();
        isLoading = false;
    }
}
