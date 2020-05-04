# current
- flip sprite renderer and stop body/collider/knockback on death

# next up
- enemy attacks

# character generation
- Creator
        - normal difficulty
        - add empty room by sacrificing self
- Destroyer 
        - hard difficulty
        - permanently destroy cleared room (costs crystals) 
- Adventurer?
        - normal difficulty (Creator becomes easy)
        - unlockable?

# bugs

# DB
- Players
        - change name to family name
        - add number of generations
        - add number of crystals
- character naming
        - upload player data to server (family name, generation count)
        - check that family name is unique
- check that coordinates for new map don't already exist (throw error)

# QOL 
- make start room look unique
- make empty rooms have no tiles 
- make it more obvious when you find an empty room
- if current room is locked when closing game, unlock room on exit
- loading screen when downloading/creating enemies for room
- yes/no text over teleporters
- how to avoid randomly generating bad words?

# combat

# visual
- remove portals during faded out after creating character
- put only family name on created enemies
- fade while moving into empty room instead of after

# major
- think about keeping players safe from ambushes set up in entrances
- multiple floors
- win condition
        - clear dungeon
        - reach location
        - amass X crystals
        - play until death
- on death:
        - character becomes permanent enemy in room where they died
        - create new character
                - some % of crystals roll over
                - OR keep a bank of crystals on death to use for something else

# jm tools
- load palette colors from palette file
- show preview colors in palette for palette+index
- warn when Health added to object with Counter
- draw reticle for where monster will be created

- power level affects: damage, accuracy, speed of bullets, movement speed
