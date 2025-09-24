from PIL import Image, ImageDraw
import os

def create_microphone_icon(size):
    """Create a microphone icon at the specified size"""
    # Create a new image with transparency
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Calculate proportions
    center_x = size // 2
    center_y = size // 2
    scale = size / 256

    # Background circle (blue)
    padding = int(8 * scale)
    draw.ellipse([padding, padding, size-padding, size-padding],
                 fill=(52, 152, 219, 255))  # #3498DB

    # Microphone body (white rounded rectangle)
    mic_width = int(40 * scale)
    mic_height = int(60 * scale)
    mic_x = center_x - mic_width // 2
    mic_y = center_y - mic_height // 2 - int(10 * scale)

    # Draw mic body
    draw.rounded_rectangle([mic_x, mic_y, mic_x + mic_width, mic_y + mic_height],
                           radius=int(mic_width//2), fill=(255, 255, 255, 255))

    # Mic grille (darker area)
    grille_width = int(24 * scale)
    grille_height = int(35 * scale)
    grille_x = center_x - grille_width // 2
    grille_y = mic_y + int(10 * scale)
    draw.rounded_rectangle([grille_x, grille_y, grille_x + grille_width, grille_y + grille_height],
                           radius=int(grille_width//2), fill=(52, 152, 219, 80))

    # Mic stand arc
    stand_y = mic_y + mic_height + int(5 * scale)
    stand_width = int(60 * scale)
    stand_height = int(20 * scale)
    stand_x = center_x - stand_width // 2

    # Draw stand (arc)
    for i in range(int(6 * scale)):
        draw.arc([stand_x - i, stand_y - i, stand_x + stand_width + i, stand_y + stand_height * 2 + i],
                 start=0, end=180, fill=(255, 255, 255, 255), width=1)

    # Mic base stem
    base_width = int(6 * scale)
    base_height = int(25 * scale)
    base_x = center_x - base_width // 2
    base_y = stand_y + stand_height
    draw.rectangle([base_x, base_y, base_x + base_width, base_y + base_height],
                   fill=(255, 255, 255, 255))

    # Mic base
    base_plate_width = int(30 * scale)
    base_plate_height = int(6 * scale)
    base_plate_x = center_x - base_plate_width // 2
    base_plate_y = base_y + base_height
    draw.rounded_rectangle([base_plate_x, base_plate_y,
                            base_plate_x + base_plate_width,
                            base_plate_y + base_plate_height],
                           radius=int(3 * scale), fill=(255, 255, 255, 255))

    return img

# Create icons at different sizes
sizes = [16, 32, 48, 64, 128, 256]
images = []

for size in sizes:
    img = create_microphone_icon(size)
    images.append(img)
    # Save individual PNG for debugging
    img.save(f'icon_{size}.png')

# Save as ICO with multiple sizes
images[0].save('VoiceLite.ico', format='ICO', sizes=[(s, s) for s in sizes])

print("Icon created successfully: VoiceLite.ico")
print(f"Icon saved to: {os.path.abspath('VoiceLite.ico')}")