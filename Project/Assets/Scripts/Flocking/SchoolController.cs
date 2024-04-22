/// SchoolController.cs
/// Author: Rohith Vishwajith
/// Created 4/21/2024

using UnityEngine;
using Unity.Mathematics;

/// <summary>
/// SchoolController:
/// Fish school simulator based on the Boids algorithm by Craig Reynolds, with additional features
/// such as target seeking and obstacle avoidance.
/// Will use the Unity Jobs system to maximize performance while keeping all data on the CPU.
/// </summary>
class SchoolController : MonoBehaviour
{
    [SerializeField][Range(1, 10000)] int spawnCount = 500;
    [SerializeField] SchoolSettings settings = null;
    [SerializeField] Transform prefab = null;
    [SerializeField] float2 spawnRange = new(5, 10);

    SchoolBoid[] entities;
    int frameCount = 0;

    // Use GPU insatncing if enabled in settings.
    Mesh instanceMesh;
    Material instanceMaterial;

    /// <summary>
    /// If mesh instancing is enabled and the instances have a mesh filter component, copy the
    /// mesh data from it and disable/remove and rendering data from the GameObject.
    /// </summary>
    void SetupMeshInstancing()
    {
        if (settings == null || !settings.useMeshInstancing || entities.Length == 0)
            return;

        // Cache the material and mesh.
        var entityTransform = entities[0].transform;
        if (entityTransform.TryGetComponent<MeshFilter>(out var meshFilter))
            instanceMesh = meshFilter.mesh;
        if (entityTransform.TryGetComponent<MeshRenderer>(out var meshRenderer))
            instanceMaterial = meshRenderer.material;

        // Disable/remove the mesh renderers for each instance.
        for (var i = 0; i < entities.Length; i++)
        {
            entities[i].transform.GetComponent<MeshRenderer>().enabled = false;
            // Destroy(entities[i].transform.GetComponent<MeshRenderer>());
        }
    }

    void Start()
    {
        if (settings == null || prefab == null)
        {
            Debug.Log("Error: No settings or prefab provided.");
            Destroy(this);
            return;
        }

        // Initialize entities[i] transform array.
        Transform[] InstantiateEntityTransforms()
        {
            var transforms = new Transform[spawnCount];
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = Instantiate(prefab).transform;
                t.name = "Entity" + i;
                var offsetDist = UnityEngine.Random.Range(spawnRange.x, spawnRange.y);
                t.position = this.transform.position + UnityEngine.Random.onUnitSphere * offsetDist;
                t.LookAt(this.transform.position, Vector3.up);
                t.forward = t.right;
                transforms[i] = t;
            }
            return transforms;
        }

        // Initialize entities[i] array.
        void CreateEntities()
        {
            var transforms = InstantiateEntityTransforms();
            entities = new SchoolBoid[spawnCount];
            for (var i = 0; i < entities.Length; i++)
            {
                var t = transforms[i];
                entities[i] = new()
                {
                    id = i,
                    position = t.position,
                    velocity = t.forward * UnityEngine.Random.Range(settings.minSpeed, settings.maxSpeed),
                    transform = t,
                    target = this.transform,
                    checkCollision = settings.enableCollisions,
                    detectedNeighbors = 0
                };
            }
        }

        // Initialize transform array than copy spatial data from each transform into the entities[i]
        // array. Replace with a matrix to avoid GameObject overhead later?
        CreateEntities();
        SetupMeshInstancing();
    }

    void Update()
    {
        // Don't update calculations if there is a lag spike (<10FPS for now), since it may cause
        // teleportation through colliders.
        if (Time.deltaTime < 0.1f)
        {
            UpdateEntityVelocities();
            MoveEntities();
        }
        UpdateTransforms();
        DrawInstances();
        frameCount += 1;
    }

    /// <summary>
    /// Apply acceleration to an entity's velocity, then reset its acceleration.
    /// TODO: Remove the acceleration field from the struct and change it to a paramter.
    /// </summary>
    /// <param name="i">The index of the entity to reset.</param>
    void ApplyAcceleration(int i)
    {
        entities[i].velocity += entities[i].acceleration * Time.deltaTime;
        if (math.length(entities[i].velocity) == 0)
        {
            entities[i].velocity = Unity.Mathematics.Random.CreateFromIndex(0).NextFloat3Direction();
        }
        var speed = math.clamp(math.length(entities[i].velocity), settings.minSpeed, settings.maxSpeed);
        entities[i].velocity = speed * math.normalize(entities[i].velocity);
        entities[i].acceleration = new();
        // entities[i].forward = math.normalize(entities[i].velocity);
    }

    /// <summary>
    /// Reset values of the temporary data in the entity which is recalculated every frame.
    /// </summary>
    /// <param name="i">Index of the entity to reset.</param>
    void ClearEntityTempData(int i)
    {
        entities[i].detectedNeighbors = 0;
        entities[i].neighborHeading = new();
        entities[i].neighborCenter = new();
        entities[i].avoidHeading = new();
        entities[i].acceleration = new();
    }

    void UpdateEntityVelocities()
    {
        // Compute acceleration for all entities.
        for (int i = 0; i < entities.Length; i++)
        {
            var detectRadius = settings.perceptionRadius;
            var avoidRadius = settings.avoidanceRadius;

            var velocity = entities[i].velocity;
            var maxSpeed = settings.maxSpeed;
            var steerForce = settings.maxSteerForce;

            // Reset all values.
            ClearEntityTempData(i);

            // Complete neighbor-based calculations.
            // FIXME: Currently this is slow, possible optimizations:
            // 1. Add colliders to transforms and use Physics.OverlapSphere()?
            // 2. Use jobs or a compute shader?
            for (int j = 0; j < entities.Length; j++)
            {
                if (i == j)
                    continue;
                var neighbor = entities[j];
                float dist = math.distance(neighbor.position, entities[i].position);
                if (dist > 0 && dist <= detectRadius)
                {
                    entities[i].detectedNeighbors += 1;
                    entities[i].neighborHeading += neighbor.forward;
                    entities[i].neighborCenter += neighbor.position;
                    if (dist <= avoidRadius)
                        entities[i].avoidHeading += (entities[i].position - neighbor.position) / (dist * dist);
                }
            }

            // If the entity has detected neighbors, update the acceleration for it.
            if (entities[i].detectedNeighbors != 0)
            {
                entities[i].neighborCenter /= entities[i].detectedNeighbors;
                // Compute align force.
                var align = settings.alignWeight * SchoolMath.SteerTowards(
                    velocity, entities[i].neighborHeading, steerForce, maxSpeed);
                // Compute center of mass (attraction) force.
                var toCenter = settings.cohesionWeight * SchoolMath.SteerTowards(
                    velocity, entities[i].neighborCenter - entities[i].position, steerForce, maxSpeed);
                // Compute separate (move away from neighbor position) force.
                var separate = settings.separateWeight * SchoolMath.SteerTowards(
                    velocity, entities[i].avoidHeading, steerForce, maxSpeed);
                // Apply forces to acceleration.
                entities[i].acceleration += align + toCenter + separate;
            }

            // Compute target attraction force if a target is set.
            if (entities[i].target != null)
            {
                var targetOffset = new float3(entities[i].target.position) - entities[i].position;
                var targetForce = settings.targetWeight * SchoolMath.SteerTowards(
                    velocity, targetOffset, steerForce, maxSpeed);
                entities[i].acceleration += targetForce;
            }
            ApplyAcceleration(i);
        }

        // Compute acceleration for collision avoidance.
        for (int i = 0; i < entities.Length; i++)
        {
            // Check if collisions should be calculated, skip RayCast if unneeded.
            if (!settings.enableCollisions)
                continue;
            else if (settings.skipCollisionFrames && (i + frameCount) % settings.collisionFrameSkips != 0)
                continue;

            var collisionWeight = settings.avoidCollisionWeight;
            var castDist = settings.collisionCheckDistance;
            var layers = settings.collisionMask;
            var steerForce = settings.maxSteerForce;
            var velocity = entities[i].velocity;
            var radius = settings.collisionCheckRadius;
            var pos = entities[i].position;
            // entities[i].transform.forward = entities[i].velocity;
            var rot = quaternion.LookRotation(math.normalize(velocity), SchoolMath.WORLD_UP);
            var dirs = SchoolMath.TURN_DIRS_MED;
            for (int k = 0; k < dirs.Length; k++)
            {
                // var dir = entities[i].transform.TransformDirection(dirs[k]);
                var dir = math.rotate(rot, dirs[k]);
                var ray = new Ray(pos, dir);
                if (radius > 0 && !Physics.SphereCast(ray, radius, castDist, layers))
                {
                    entities[i].acceleration += collisionWeight * SchoolMath.SteerTowards(
                        velocity, dir, steerForce, settings.maxSpeed);
                    break;
                }
                else if (radius == 0 && !Physics.Raycast(ray, castDist, layers))
                {
                    entities[i].acceleration += collisionWeight * SchoolMath.SteerTowards(
                        velocity, dir, steerForce, settings.maxSpeed);
                    break;
                }
            }
            ApplyAcceleration(i);
        }
    }

    void UpdateEntityVelocitiesInParallel()
    {

    }

    /// <summary>
    /// Update the position and heading of each velocity. Assumes velocity.length > 0.
    /// </summary>
    void MoveEntities()
    {
        for (var i = 0; i < entities.Length; i++)
        {
            var nextPos = entities[i].position + entities[i].velocity * Time.deltaTime;
            entities[i].position = nextPos;
        }
    }

    /// <summary>
    /// Apply the updated entities[i] data onto the corresponding GameObjects.
    /// TODO: Preferably replace this with matrices later to reduce overhead when moving to a
    /// DOTS-based system.
    /// </summary>
    void UpdateTransforms()
    {
        for (var i = 0; i < entities.Length; i++)
        {
            entities[i].transform.position = entities[i].position;
            entities[i].transform.forward = entities[i].forward;
        }
    }

    void DrawInstances()
    {
        if (!settings.useMeshInstancing)
            return;
        if (instanceMaterial == null || instanceMesh == null)
            return;
        var renderParams = new RenderParams(instanceMaterial);
        var meshesRendered = 0;
        var step = SchoolMath.MAX_INSTANCE_COUNT;
        for (var i = 0; i < entities.Length; i += step)
        {
            var instanceData = new Matrix4x4[Mathf.Min(i + step, entities.Length) - i];
            for (var j = i; j < math.min(i + instanceData.Length, entities.Length); j++)
            {
                instanceData[j] = entities[i + j].transform.localToWorldMatrix;
                meshesRendered += 1;
            }
            Graphics.RenderMeshInstanced(renderParams, instanceMesh, 0, instanceData);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new(Color.cyan.r, Color.cyan.g, Color.cyan.b, 0.1f);
        Gizmos.DrawSphere(transform.position, spawnRange.x);
        Gizmos.DrawSphere(transform.position, spawnRange.y);
    }
}