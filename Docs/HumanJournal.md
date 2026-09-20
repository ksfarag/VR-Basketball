# Developer journal

Record your decisions and firsthand observations here. Leave an AI-assisted entry marked as a draft until you have reviewed it. Planned behavior and automated checks are not personal playtest results.

## Foundation decisions

**AI-assisted draft — pending developer review.**

I want a small VR basketball game with satisfying throws, clear scoring, and useful feedback. I chose to establish the project structure, tools, and verification process before building gameplay. Grabbing, throwing, and any movement should remain understandable project code. I chose to retain Unity AI Assistant and explore a desktop Operator setup compatible with the fixed Unity version.

I supplied build and Simulator diagnostics so the setup problems could be investigated. The scaffold build and desktop XR checks succeeded, but I have not yet evaluated gameplay on the headset. My next development priorities are the input/rig foundation, one ball and hoop, scoring, reset, and playtest tuning. I will review changes before deciding what to commit or publish.

For future entries, record what you decided or tried, what you actually observed, feedback you gave, and the related [AI log](../AI_LOG.md) entry or evidence.

## First scoring playtest

**AI-assisted draft — pending developer review.**

I played the scene with scoring in place and scored. I noticed the board gave me two points for one basket and asked whether that was right; it is the `pointsPerBasket` default of 2, a field goal, and the basket itself was counted once.

What I actually found was that scoring at all was hard: the hoop needed to be bigger and it was too high. I asked for both to change. See the [AI log](../AI_LOG.md) entry "Playtest: the regulation hoop was too hard to score on" for what was changed and by how much.

I have not yet said whether the score is readable while shooting, whether the chime lands at the right moment, or whether the flash is noticeable.

## Scoring and reset work on the headset

**AI-assisted draft — pending developer review.**

I tested in VR and it passes. That covers the scoring and reset work from plan items 4 and 5; before this, neither had been confirmed on a device.

I asked for the hoop to be a little bigger again, having played the version with a 0.6 m hole at 2.7 m. See the [AI log](../AI_LOG.md) entry "Headset: scoring and reset pass, and the hoop goes up again".

I have not yet given an opinion on throw feel, whether the score is readable while shooting, whether the chime lands at the right moment, whether the flash is noticeable, or whether holding B to clear the score matches what I expect.
