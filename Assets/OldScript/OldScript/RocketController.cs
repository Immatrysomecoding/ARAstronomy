using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RocketController : MonoBehaviour
{
    public ParticleSystem boom;
    public Transform fakeRocket;
    public AudioClip boomAudio;
    public AudioClip correctSound;
    public AudioClip failSound;
    public AudioClip gameOverSound;
    public AudioClip warningSound;
    public Level level;
    public GameObject Render;
    private bool endGame = false;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private IEnumerator coroutine = null;
    public void FlyTo(Transform target, bool finalFly = false)
    {
        if (endGame) return;
        AudioManager.Instance.audioSource.PlayOneShot(failSound);
        if (finalFly)
        {
            Cancel();
            StartCoroutine(CouroutineFlyTo(target));
        }
            
        else
        {
            Cancel();
            coroutine = AnimationFLy(target);
            StartCoroutine(coroutine);
        }
    }
    
    public void Cancel()
    {
        if (coroutine != null)
        {
            fakeRocket.gameObject.SetActive(false);
            StopCoroutine(coroutine);
        }
    }
    IEnumerator AnimationFLy(Transform target, float time = 1.0f)
    {
        fakeRocket.gameObject.SetActive(true);
        fakeRocket.position = transform.position;
        Vector3 startingPos = fakeRocket.transform.position;
        Vector3 finalPos = (target.position - startingPos);
        var length = finalPos.magnitude;
        finalPos = startingPos + finalPos.normalized * length / 3;
        float elapsedTime = 0;
        AudioManager.Instance.audioSource.PlayOneShot(warningSound);
        while (elapsedTime < time)
        {
            fakeRocket.transform.position = Vector3.Lerp(startingPos, finalPos, (elapsedTime / time));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        fakeRocket.gameObject.SetActive(false);
        coroutine = AnimationFLy(target);
        StartCoroutine(coroutine);
    }
    IEnumerator CouroutineFlyTo(Transform target, float time = 2.0f)
    {
        endGame = true;
        Vector3 startingPos = transform.position;
        Vector3 finalPos = target.position;
        float elapsedTime = 0;
        AudioManager.Instance.audioSource.PlayOneShot(warningSound);
        while (elapsedTime < time)
        {
            transform.position = Vector3.Lerp(startingPos, finalPos, (elapsedTime / time));
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        AudioManager.Instance.audioSource.PlayOneShot(boomAudio);
        boom.gameObject.SetActive(true);
        boom.Play();
        yield return new WaitForSeconds(1f);
        AudioManager.Instance.audioSource.PlayOneShot(gameOverSound);
        Render.SetActive(false);
        level.EndLevel(false);
        boom.gameObject.SetActive(false);

    }
    public void NotifySuccess()
    {
        print("success");
        AudioManager.Instance.audioSource.PlayOneShot(correctSound);
        level.EndLevel(true);
        boom.gameObject.SetActive(false);
    }
    public void ResetGame()
    {
        endGame = false;
        Render.SetActive(true);
    }
    private void OnTriggerEnter(Collider other)
    {
        print(other.name);
    }

}
