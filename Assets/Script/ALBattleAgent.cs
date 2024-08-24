using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

namespace Footsies
{
    public class ALBattleAgent : Agent
    {
        //This code was copied from the pre-existing battle AI script
        //This script allow p2 to be controlled by the ML-agents artifical brain object and participate in adversarial ML training
        public class FightState
        {
            public float distanceX;
            public bool isOpponentDamage;
            public bool isOpponentGuardBreak;
            public bool isOpponentBlocking;
            public bool isOpponentNormalAttack;
            public bool isOpponentSpecialAttack;
        }

        private BattleCore battleCore;
        private Fighter fighter;

        private Queue<int> moveQueue = new Queue<int>();
        private Queue<int> attackQueue = new Queue<int>();

        // previous fight state data
        private FightState[] fightStates = new FightState[maxFightStateRecord];
        public static readonly uint maxFightStateRecord = 10;
        private int fightStateReadIndex = 5;

        private SensorComponent sensor;

        public override void Initialize()
        {
            base.Initialize();
            battleCore = GetComponentInParent<BattleCore>();
            sensor = GetComponent<SensorComponent>();
            fighter = battleCore.fighter2;
        }



        // Start is called before the first frame update
        void Start()
        {
            fighter = battleCore.fighter2;
        }

        public override void OnEpisodeBegin()
        {
            fighter = battleCore.fighter2;
            //battleCore.resetRoundState();
            moveQueue.Clear();

        }

        public override void CollectObservations(VectorSensor sensor)
        {
            //Debug.Log("collect observations");
            //sense the current Fightstate and send it to the ML policy

            //sense the agent's current position
            sensor.AddObservation(fighter.position.x);
            sensor.AddObservation(fighter.velocity_x);
            //sense the distance between fighters
            sensor.AddObservation(Mathf.Abs(battleCore.fighter1.position.x - battleCore.fighter2.position.x));

            //sense the current state of the enemy
            //is the oppenent damaged
            sensor.AddObservation(battleCore.fighter1.currentActionID == (int)CommonActionID.DAMAGE);
            //is the oppenent's guard broken
            sensor.AddObservation(battleCore.fighter1.currentActionID == (int)CommonActionID.GUARD_BREAK);
            //is the oppenent blocking
            sensor.AddObservation(battleCore.fighter1.currentActionID == (int)CommonActionID.GUARD_CROUCH
                                                    || battleCore.fighter1.currentActionID == (int)CommonActionID.GUARD_STAND
                                                    || battleCore.fighter1.currentActionID == (int)CommonActionID.GUARD_M);
            //is the oppenent using a normal attack
            sensor.AddObservation(battleCore.fighter1.currentActionID == (int)CommonActionID.N_ATTACK
                                                    || battleCore.fighter1.currentActionID == (int)CommonActionID.B_ATTACK);
            //is the oppenent using a special attack
            sensor.AddObservation(battleCore.fighter1.currentActionID == (int)CommonActionID.N_SPECIAL
                                                    || battleCore.fighter1.currentActionID == (int)CommonActionID.B_SPECIAL);

        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            //actions
            //actions are the output of the ML-agents policy based on the current Fightstate
            int movementCommand = actions.DiscreteActions[0];
            int attackCommand = actions.DiscreteActions[1];

            //Debug.Log(actions.DiscreteActions[0]);
            //Debug.Log(actions.DiscreteActions[1]);

            //Only add new inputs if the input queue is empty.
            if (moveQueue.Count == 0)
            {
                switch (movementCommand)
                {
                    case 0:
                        AddNeutralMovement();
                        break;
                    case 1:
                        AddForwardInputQueue(1);
                        break;
                    case 2:
                        AddForwardDashInputQueue();
                        break;
                    case 3:
                        AddBackwardInputQueue(1);
                        break;
                    case 4:
                        AddBackwardDashInputQueue();
                        break;
                }

                switch (attackCommand)
                {
                    case 0:
                        AddNoAttack();
                        break;
                    case 1:
                        AddOneHitImmediateAttack();
                        break;
                    case 2:
                        AddTwoHitImmediateAttack();
                        break;
                    case 3:
                        AddImmediateSpecialAttack();
                        break;
                }
            }

            //Add a small negative reward for each frame of the fight to encurage the agent to win faster and avoid turtling in the corner
            AddReward(-0.00001f);

            if (fighter.isDead)
            {
                //Debug.Log("round Lost");
                AddReward(-7f);
                moveQueue.Clear();
                attackQueue.Clear();

                //Note: Comment out the next line when building footies.exe to ensure that the rounds end as normal
                battleCore.resetRoundState();
                EndEpisode();
            }

            if (battleCore.fighter1.isDead)
            {
                //Debug.Log("round won!");
                AddReward(10f);
                moveQueue.Clear();
                attackQueue.Clear();

                //Note: Comment out the next line when building footies.exe to ensure that the rounds end as normal
                battleCore.resetRoundState();
                EndEpisode();
            }
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var discreteActions = actionsOut.DiscreteActions;
            //Debug.Log("heuristic");

            if (Input.GetButton("left"))
            {

                discreteActions[0] = 2;
            }
            else if (Input.GetButton("right"))
            {

                discreteActions[0] = 1;
            }
            else
            {

                discreteActions[0] = 0;
            }

            if (Input.GetButton("right shift"))
            {

                discreteActions[1] = 1;
            }
            else
            {

                discreteActions[1] = 0;
            }

        }

        //This function is called by the battlecore at each frame to get the input command for the AI agent.
        public int getAgentInput()
        {
            InputData agentInput = new InputData();

            if (moveQueue.Count > 0)
                agentInput.input |= moveQueue.Dequeue();

            if (attackQueue.Count > 0)
                agentInput.input |= attackQueue.Dequeue();

            //Debug.Log("send AI input");
            return agentInput.input;
        }

        //pre-defined actions from the existing AI.
        private int GetAttackInput()
        {
            return (int)InputDefine.Attack;
        }

        private int GetForwardInput()
        {
            return (int)InputDefine.Right;
        }

        private int GetBackwardInput()
        {
            return (int)InputDefine.Left;
        }

        private void AddNeutralMovement()
        {
            for (int i = 0; i < 5; i++)
            {
                moveQueue.Enqueue(0);
            }

            //Debug.Log("AddNeutral");
        }

        private void AddNoAttack()
        {
            for (int i = 0; i < 17; i++)
            {
                attackQueue.Enqueue(0);
            }

            //Debug.Log("AddNoAttack");
        }

        private void AddOneHitImmediateAttack()
        {
            attackQueue.Enqueue(GetAttackInput());
            for (int i = 0; i < 16; i++)
            {
                attackQueue.Enqueue(0);
            }

            //Debug.Log("AddOneHitImmediateAttack");
        }

        private void AddTwoHitImmediateAttack()
        {
            attackQueue.Enqueue(GetAttackInput());
            for (int i = 0; i < 3; i++)
            {
                attackQueue.Enqueue(0);
            }
            attackQueue.Enqueue(GetAttackInput());
            for (int i = 0; i < 18; i++)
            {
                attackQueue.Enqueue(0);
            }

            //Debug.Log("AddTwoHitImmediateAttack");
        }

        private void AddImmediateSpecialAttack()
        {
            for (int i = 0; i < 60; i++)
            {
                attackQueue.Enqueue(GetAttackInput());
            }
            attackQueue.Enqueue(0);

            //Debug.Log("AddImmediateSpecialAttack");
        }

        private void AddDelaySpecialAttack()
        {
            for (int i = 0; i < 120; i++)
            {
                attackQueue.Enqueue(GetAttackInput());
            }
            attackQueue.Enqueue(0);

            //Debug.Log("AddDelaySpecialAttack");
        }

        private void AddForwardInputQueue(int frame)
        {
            for (int i = 0; i < frame; i++)
            {
                moveQueue.Enqueue(GetForwardInput());
            }
        }

        private void AddBackwardInputQueue(int frame)
        {
            for (int i = 0; i < frame; i++)
            {
                moveQueue.Enqueue(GetBackwardInput());
            }
        }

        private void AddForwardDashInputQueue()
        {
            moveQueue.Enqueue(GetForwardInput());
            moveQueue.Enqueue(0);
            moveQueue.Enqueue(GetForwardInput());
        }

        private void AddBackwardDashInputQueue()
        {
            moveQueue.Enqueue(GetBackwardInput());
            moveQueue.Enqueue(0);
            moveQueue.Enqueue(GetBackwardInput());
        }
    }
}
