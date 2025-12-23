using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject character;
    [SerializeField] private ParticleSystem deathParticles;
    [SerializeField] private AudioSource deathSound;


    private void OnCollisionEnter(Collision collision)
    {
        // Only collide with vehicles if we're not already done so.
        if(collision.gameObject.CompareTag("Vehicle") && character.activeSelf)
        {
            // Get the contact point of the collision
            Kill(collision.GetContact(0).point);
        }
    }

    public void Kill(Vector3 collisionPoint)
    {
        // Hide the character model
        character.SetActive(false);

        // Orient the particle relative to the collision
        deathParticles.transform.position = collisionPoint;
        deathParticles.transform.LookAt(transform.position + Vector3.up);

        // Show the particle effect
        deathParticles.Play(true);
        deathSound?.Play();

        // Tell game manager we collided.
        gameManager.PlayerCollision();
    }

    public void Reset()
    {
        //Re-enable the character model
        character.SetActive(true);

        // Remove any left over particles
        deathParticles.Clear();
    }
}
