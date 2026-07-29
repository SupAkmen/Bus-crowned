using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class RVOSimulator : MonoBehaviour
{
    public static RVOSimulator Instance;

    List<RVOAgent> agents = new();

    private void Awake()
    {
        Instance = this;
    }

    public void Register(RVOAgent agent)
    {
        if (!agents.Contains(agent))
            agents.Add(agent);
    }

    public void Unregister(RVOAgent agent)
    {
        agents.Remove(agent);
    }

    private void FixedUpdate()
    {
        FindNeighbors();

        foreach(RVOAgent agent in agents)
        {
            ComputeVelocity(agent);
        }

        foreach(RVOAgent agent in agents)
        {
            Vector3 nextPos  = agent.transform.position + agent.velocity * Time.deltaTime;

            nextPos = MovementBoundary.Instance.Clamp(nextPos);

            agent.transform.position = nextPos;
        }    
            
    }

    // tim cac agent gan la hang xom
    void FindNeighbors() 
    {
        foreach(RVOAgent agent in agents)
        {
            agent.neighbors.Clear();

            foreach(RVOAgent other in agents)
            {
                if(agent == other)
                {
                    continue;
                }

                float sqrtDistance = (agent.transform.position - other.transform.position).sqrMagnitude;

                if (sqrtDistance <= agent.neighborDistance * agent.neighborDistance)
                {
                    agent.neighbors.Add(other);
                }
            }
        }
    }

    // tinh luc tranh giua hai agent

    void ComputeVelocity(RVOAgent agent)
    {
        Vector3 avoidance = Vector3.zero;

        foreach (RVOAgent other in agent.neighbors)
        {
            float minDistance = agent.radius + other.radius;

            // Xư li va cham thuc te hien tai. Neu hai agent de len nhau o hien tai thi se day manh ra va uu tien hon avoidance du doan ben duoi
            // giup giai quyet overlap nahnh va dut khoat hon

            Vector3 currentOffset = agent.transform.position - other.transform.position;

            currentOffset.y = 0f;

            float currentDistance = currentOffset.magnitude;

            if(currentDistance < minDistance)
            {
                if(currentDistance < 0.0001f)
                {
                    currentOffset = Random.insideUnitCircle;
                    currentOffset.y = 0;
                    currentDistance = 0.0001f;
                }

                float overlapStrength = (minDistance - currentDistance) / minDistance;

                // Nhan 2 de uu tien xu ly va chma thuc te hon la du doan
                avoidance += currentOffset.normalized * overlapStrength * 1.2f;
            }

            // Du doan va cham trong tuong lai
            Vector3 futureA = agent.transform.position + agent.velocity * agent.timeHorizon;
 
            Vector3 futureB = other.transform.position + other.velocity * other.timeHorizon;

            Vector3 offset = futureA - futureB;

            float distance = offset.magnitude;

            

            // Neu trong tuong lai co va cham

            if(distance < minDistance)
            {
                if(distance < 0.0001f)
                {
                    offset = Random.insideUnitCircle;
                    offset.y = 0;
                    distance = 0.001f;
                }

                float strength = (minDistance - distance) / minDistance;

                avoidance += offset.normalized * strength;
            }
        }

        avoidance *= agent.seperationWeight;

        Vector3 pushForce = Vector3.zero;

        foreach(RVOAgent other in agent.neighbors)
        {
            Vector3 offset = agent.transform.position - other.transform.position;
            offset.y = 0f;

            float distance = offset.magnitude;

            if(distance < 0.001f)
            {
                offset = Random.insideUnitCircle;
                offset.y = 0;
                distance = 0.001f;
            }

            float safeDistance = agent.pushDistance + other.radius;

            if(distance < safeDistance)
            {
                // 0 -> 1
                float t = 1f - distance/safeDistance;

                // Smooth giup luc tang dan

                t = t * t * (3f - 2f * t);

                pushForce += offset.normalized * t * agent.pushStrength;
            }
        }


        Vector3 desired = agent.preferredVelocity + avoidance + pushForce;


        // Toc do phan hoi de agent ra nhanh hon khi va cham : 10f 
        agent.velocity = Vector3.Lerp(agent.velocity,desired,8f * Time.fixedDeltaTime);

        agent.velocity = Vector3.ClampMagnitude(agent.velocity, agent.maxSpeed);
    }
}
