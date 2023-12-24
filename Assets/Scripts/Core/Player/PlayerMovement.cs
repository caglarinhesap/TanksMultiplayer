using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Transform bodyTransform;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private ParticleSystem dustCloud;

    [Header("Settings")]
    [SerializeField] private float movementSpeed = 4f;
    [SerializeField] private float turningRate = 30f;
    [SerializeField] private float particleEmissionValue = 10;

    private ParticleSystem.EmissionModule emissionModule;
    private Vector2 lastMovementInput;
    private Vector3 previousPos;

    private const float ParticleStopThreshold = 0.005f;

    private void Awake()
    {
        emissionModule = dustCloud.emission;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        inputReader.MoveEvent += HandleMove;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        inputReader.MoveEvent -= HandleMove;
    }

    private void Update()
    {
        if (!IsOwner) return;

        float zRotation = lastMovementInput.x * -turningRate * Time.deltaTime;
        bodyTransform.Rotate(0, 0, zRotation);
    }

    private void FixedUpdate()
    {
        if ((transform.position - previousPos).sqrMagnitude > ParticleStopThreshold)
        {
            emissionModule.rateOverTime = particleEmissionValue;
        }
        else
        {
            emissionModule.rateOverTime = 0;
        }

        previousPos = transform.position;

        if (!IsOwner) return;

        rb.velocity = (Vector2)bodyTransform.up * lastMovementInput.y * movementSpeed;
    }

    private void HandleMove(Vector2 movementInput)
    {
        lastMovementInput = movementInput;
    }
}
