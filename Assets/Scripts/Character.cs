using UnityEngine;

public class Character : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject character;
    [SerializeField] private ParticleSystem deathParticles;
    [SerializeField] private AudioSource deathSound;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Vehicle") && character.activeSelf)
        {
            gameManager.Die(DeathType.VehicleCollision);
        }
    }

    public void PlayDeathFeedback(Vector3 collisionPoint)
    {
        character.SetActive(false);

        deathParticles.transform.position = collisionPoint;
        deathParticles.transform.LookAt(transform.position + Vector3.up);
        deathParticles.Play(true);
        deathSound?.Play();
    }

    public void ResetCharacter()
    {
        character.SetActive(true);

        if (deathParticles != null)
        {
            deathParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            deathParticles.Clear();
        }

        if (deathSound != null)
        {
            deathSound.Stop();
        }
    }
}
