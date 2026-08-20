# AI CONSTITUTION

*Civilization Simulation AI Architecture and Behavioural Constitution*

Planning document for M5 and subsequent AI milestones. This defines architectural intent and boundaries, not an implementation packet.

## 1. Executive Decision

Build one general civilization decision architecture rather than separate hard-coded AIs or large bespoke decision trees.

AI behaviour is the product of six layers:

- Decision Engine: observes state, generates feasible actions, evaluates consequences, and selects an action.
- Goals and Priorities: what the civilization currently cares about.
- Personality: persistent preferences that change the weighting of competing objectives.
- Competence: how effectively the AI reasons, plans, predicts, learns, and executes.
- Information State: what the AI actually knows, estimates, or believes.
- Internal History: accumulated experience, confidence, threat perception, grievances, relationships, and adaptation.

Difficulty primarily changes competence. Personality primarily changes priorities. Information must respect the game's AI-symmetry constitution. Higher difficulty must not rely on hidden resources or simulation-rule advantages.

## 2. Design Goals

- Autonomous civilizations requiring no player micromanagement.
- Different civilizations behave differently under the same rules.
- The player can infer intentions, priorities, strengths, weaknesses, and history.
- Mistakes arise from limited competence, uncertainty, bad judgment, or personality, not arbitrary scripts.
- Higher difficulty produces better decisions rather than economic cheating.
- The architecture scales from basic M5 behaviour to sophisticated adversarial behaviour without fundamental redesign.
- AI remains a participant in the simulation rather than an exception to it.

## 3. Non-Goals

- Do not make an enormous if/then tree the primary architecture.
- Do not create one AI implementation per civilization.
- Do not encode personality as direct commands such as 'Aggressive = attack neighbour'.
- Do not make difficulty primarily a resource, population, production, combat, or information bonus.
- Do not give omniscient access to hidden simulation state.
- Do not create a separate economic simulation for AI.
- Do not use randomness as a substitute for reasoning.

## 4. Core Decision Loop

SIMULATION STATE → OBSERVATION → INFORMATION/BELIEF STATE → NEEDS/GOALS → CANDIDATE ACTIONS → CONSEQUENCE EVALUATION → PERSONALITY/PRIORITY WEIGHTING → COMPETENCE/RISK FILTER → ACTION SELECTION → EXECUTION → OBSERVED OUTCOME → INTERNAL UPDATE

### 4.1 Observation

AI observes the same categories of reality available to an equivalent player, subject to reconnaissance, discovery, diplomacy, and other information rules.

### 4.2 Goals

Goals arise from actual conditions: food security, territorial security, infrastructure, population, military preparation, diplomacy, technology, economic resilience, and strategic opportunities.

### 4.3 Candidate generation

Generate plausible legal actions. Do not score impossible actions.

### 4.4 Evaluation

Evaluate competing consequences across objectives. An action can help one objective while harming another.

### 4.5 Selection

Selection may incorporate uncertainty, risk tolerance, confidence, and controlled stochasticity. Personality should affect evaluation, not replace it.

## 5. Personality Architecture

Personality is a continuous behavioural preference model, not a script.

- Expansionism
- Military priority
- Risk tolerance
- Commercial orientation
- Food/population security
- Diplomatic preference
- Trust
- Prestige seeking
- Institution building
- Technological appetite
- Isolationism
- Long-term orientation
- Opportunity exploitation
- Tolerance for domestic hardship

Prefer continuous values over purely categorical labels. Archetypes may be derived for presentation or generation, but must not become hidden behaviour trees.

### 5.1 Personality does not dictate action

Expansionism increases the value of successful territorial expansion. It does not force an attack. An expansionist state surrounded by stronger enemies may rationally remain defensive.

### 5.2 Procedural combinations

Generate personalities from parameter distributions and constraints. Commercial, cautious, diplomatic and militaristic, risk-tolerant, expansionist civilizations can use the same brain.

### 5.3 Stability and adaptation

Core personality should be relatively persistent. A separate internal-state layer can modify effective priorities as history accumulates.

## 6. Competence and Difficulty

Difficulty is primarily decision quality, not material advantage.

- Planning horizon
- Candidate breadth
- Forecast quality
- Opponent modelling
- Opportunity recognition
- Threat recognition
- Trade-off reasoning
- Execution quality
- Learning/adaptation
- Risk calibration

### 6.1 Suggested ladder

- Easy: reactive, short horizon, limited alternatives, weak forecasting and slow adaptation.
- Normal: competent baseline and sensible responses to major problems.
- Hard: longer planning, stronger opportunity/threat recognition and trade-off analysis.
- Expert: multi-domain planning, stronger opponent modelling and contingency planning.
- Master: long-horizon planning, adversarial reasoning, and strong structural opportunity exploitation.

These are targets for progression, not a demand that M5 implement everything.

## 7. Information and Belief

The AI must distinguish reality from what it knows about reality.

- Observed facts
- Known facts from legitimate channels
- Estimates
- Predictions
- Uncertainty
- Unknown information

A stronger AI should not automatically receive hidden information. It should become better at acquiring, retaining, interpreting, and acting on information it is permitted to obtain.

AI symmetry remains a hard constraint: player-identical verbs and information classes, with difficulty arising primarily from information and decision quality.

## 8. Internal State and Memory

- Threat perception by civilization
- Trust and diplomatic expectations
- Grievances
- Recent losses and victories
- Confidence in military/economic capability
- Failed strategies
- Successful strategies
- Opponent expectations
- Current strategic priorities
- Unresolved problems

Maintain bounded strategic memory rather than a complete event transcript.

## 9. Adaptive Behaviour

A civilization should change strategy without changing its fundamental identity. A commercial civilization repeatedly blockaded may become more security-focused while remaining commercial.

Prefer emergent adaptation over arbitrary rules such as 'after two wars become militaristic' unless later mechanisms explicitly justify such a rule.

## 10. Strategic Objectives

- Prevent food insecurity
- Maintain territorial security
- Exploit trade opportunities
- Increase productive capacity
- Resolve demographic imbalance
- Protect strategic settlements
- Exploit a weakened neighbour
- Recover from war
- Build institutions
- Maintain domestic stability
- Increase prestige/influence

Objectives compete. Trade-offs create interesting behaviour.

## 11. Opportunity vs Crisis

Distinguish responding to an immediate problem from pursuing an opportunity. Competent AI should be able to recognize when solving a crisis also creates strategic leverage.

## 12. Risk and Uncertainty

Risk tolerance is personality; risk assessment is competence. A reckless but competent AI understands an action is dangerous and chooses it anyway. An incompetent AI may choose it because it failed to understand the danger.

## 13. Stochasticity

Controlled randomness may prevent identical behaviour across runs. Randomness should perturb rational candidate choices rather than replace reasoning. Use the engine's deterministic random infrastructure where reproducibility is required.

## 14. Civilization Identity

Identity emerges from base personality + current priorities + competence + information + history + relationships + geography/resources + demographic/institutional circumstances.

Avoid name-plus-script identities.

## 15. Civilization Interaction

- Relative strength
- Relative vulnerability
- Reliability
- Trade value
- Diplomatic value
- Threat
- Historical grievance
- Strategic opportunity
- Likely response to an action

Other civilizations are actors, not resource containers. This is the foundation for diplomacy, coercion, alliances, competition, and war.

## 16. Anti-Cheating Constitution

- No hidden resource generation because an AI is difficult.
- No hidden population, food, production, military, or economic advantages as the normal difficulty mechanism.
- No hidden map knowledge unavailable to an equivalent player.
- No AI-only action impossible for the player.
- No silent causal-mechanism changes at higher difficulty.
- Any explicit handicap must be documented and justified separately from competence.

## 17. Failure Modes

- Decision-tree explosion
- Personality-as-script
- Difficulty-as-cheating
- Omniscience
- Strategic oscillation
- Local optimization that destroys broader position
- Unbounded planning cost
- Personality collapse into identical optimal behaviour
- Random stupidity
- Scripted historical determinism

## 18. M5 Minimum Viable AI

- Autonomously observe state.
- Identify basic needs and opportunities.
- Generate a bounded set of legal actions.
- Evaluate actions through a common framework.
- Execute basic economic, settlement, diplomatic, and strategic actions.
- Operate without player intervention.
- Produce measurable behavioural differences from personality.
- Produce better decisions rather than simply more resources at higher difficulty.
- Respect information boundaries.
- Remain reproducible under engine requirements.

## 19. M5 Should Not Attempt Yet

- Perfect long-horizon planning
- Human-level opponent modelling
- Sophisticated deception
- Deep military tactics before combat systems exist
- Machine learning trained on massive game corpora
- LLM reasoning directly inside the simulation loop
- Hundreds of bespoke personality scripts
- Complete diplomacy before diplomacy mechanisms exist

## 20. Measurement and Testing

AI quality must be measurable, not judged from one playthrough.

- Action diversity by personality
- Strategic objective diversity
- Planning horizon by difficulty
- Crisis response time
- Frequency of obviously dominated actions
- Resource/population trajectories
- War initiation/avoidance by personality
- Trade behaviour
- Adaptation after success/failure
- Information-use errors under restricted visibility
- Determinism under fixed seeds
- Decision latency

Acceptance thresholds should be established by implementation packets from actual system behaviour rather than invented here.

## 21. Debugging and Explainability

Engineering must be able to answer: 'Why did the AI do that?'

- Current goals
- Candidate actions considered
- Major evaluation factors
- Personality weights involved
- Relevant information and uncertainty
- Expected outcomes
- Selected action
- Why materially better alternatives were rejected, where applicable

This is primarily an engineering/debugging requirement and does not require exposing all AI state to players.

## 22. Mechanisms Over Modifiers

AI should interact with simulation mechanisms rather than bypass them. If a civilization wants more steel, its AI should influence conditions that make steel production emerge or expand. It should not directly apply 'steel +20%' because steel is a priority.

The AI therefore participates in the same causal systems as the player.

## 23. Relationship to Existing Constitution

- AI symmetry: player-identical verbs and information class; difficulty should be information and decision quality, not hidden rule asymmetry.
- Mechanisms over modifiers: influence conditions and actions, do not force outcomes.
- Emergent systems: operate through the same causal systems as the player.
- Information systems: respect reconnaissance, discovery, diplomacy and established information boundaries.
- Strategic dt: AI decisions occur at the authoritative strategic timestep unless a sanctioned crisis layer exists.

Before implementation, the coder must verify these references against the current repository. If this document conflicts with ratified architecture, the tree and ratified documents win and the conflict is a finding.

## 24. Future Evolution

- M5: basic autonomous decision-making.
- M5+: procedural personalities and competence tiers.
- M6+: deeper military and diplomatic reasoning as those systems become available.
- Later: opponent modelling, forecasting, adaptation and richer institutional behaviour.
- End state: civilizations that are different, coherent, historically contingent, and capable of surprising the player without cheating.

## 25. Intended Player Experience

- I understand what this civilization values.
- I can see why it made this decision.
- It has strengths and weaknesses.
- Its behaviour reflects its history.
- It sometimes surprises me.
- It can make mistakes.
- It can learn.
- It can exploit my mistakes.
- It does not possess information or resources it should not have.
- I am competing with an independent political actor rather than watching a script.

## 26. Implementation Freedom

This document defines outcomes, principles, responsibilities, and constraints, not exact classes, algorithms, constants, or file structure.

The implementation agent is expected to make reasonable engineering choices inside this fence. It should not stop merely because this document does not specify every implementation detail.

When a mechanism is required but unspecified: inspect the repository, inspect ratified decisions and precedents, identify the smallest consistent mechanism, document the decision, test it, and proceed. Escalate only for genuine frozen-architecture conflicts, director-level policy choices, or irreversible scope decisions.

## 27. Director-Level Decisions Reserved

- Strategic capabilities targeted for each milestone
- Whether a behaviour violates intended player experience
- Whether an explicit handicap is permitted
- Whether a new information asymmetry is acceptable
- Any change to frozen simulation law or architecture
- Any major new system not derivable from current architecture

## 28. Final Rule

Build one brain. Give civilizations different values. Give difficulties different competence. Give every civilization imperfect information. Let history change what it believes. Let the simulation determine what actually happens.

The objective is not to make AI appear intelligent through scripted tricks. The objective is to create civilizations whose decisions emerge from the same world, rules, constraints, information, and causal systems as the player.
