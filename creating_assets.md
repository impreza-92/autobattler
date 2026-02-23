# Creating Assets: Sprites & Spritesheets for Godot Autobattle

This guide outlines what to consider when creating sprites and spritesheets for units, items, and other assets in your Godot-based autobattle game. It covers both general best practices and Godot-specific requirements, referencing your project’s architecture and Godot conventions.

## 1. Sprite Design Considerations
- Use consistent resolution and aspect ratio for all sprites (e.g., 32x32, 64x64, or 128x128 pixels).
- Maintain a uniform art style and color palette across units, items, and effects.
- Design sprites with transparent backgrounds (PNG format recommended).
- For animation, create spritesheets with frames arranged in a grid (horizontal or vertical).

## 2. Spritesheet Structure
- Organize animation frames in rows/columns; each frame should be the same size.
- Name files clearly (e.g., unit_knight.png, item_potion.png).
- Include metadata (frame count, frame size, animation type) if needed for automated import.

## 3. Godot-Specific Requirements
- Godot supports both single sprites and spritesheets (AnimatedSprite2D, Sprite2D nodes).
- Spritesheets should be imported as PNGs; Godot can auto-detect frame size if set in the inspector.
- For AnimatedSprite2D, set up animations by specifying frame size and frame count.
- Use Godot’s import settings to enable “Filter” (for smooth scaling) or disable for pixel art.
- Keep file paths organized under Scenes/ or a dedicated assets folder.

## 4. Engine/UI Separation
- Store sprite references in data (e.g., unit archetype or item registry) as string paths, not direct object references.
- UI scripts in Scenes/ should load sprites based on engine state and event data, never from engine internals.

## 5. Extensibility & Data-Driven Approach
- Add new sprites by updating data registries (e.g., ArchetypeRegistry, AbilityRegistry) with new sprite paths.
- Avoid hardcoding sprite logic; use IDs and paths for lookup.

## Verification
- Import sample spritesheets into Godot and assign to AnimatedSprite2D nodes.
- Test frame setup in the inspector (frame size, count, animation names).
- Confirm sprites display correctly for units/items in the UI, triggered by engine events.
- Check that adding new sprites only requires updating data registries, not code.

## Decisions
- Use PNG format for transparency and compatibility.
- Organize spritesheets with consistent frame size and clear naming.
- Reference sprites by path/ID in data registries for easy extension.

---
For further detail or guidance on asset management structure, consult the project architecture guide or ask for specific examples.