from PIL import Image, ImageDraw
import os

def create_microphone_icon(size):
    """Create a minimalist microphone icon at the specified size (Apple-style)"""
    # Create a new image with transparency
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Calculate proportions based on 200x200 viewBox
    scale = size / 200

    # VoiceLite purple color
    purple = (124, 58, 237, 255)  # #7c3aed

    # Mic capsule - rounded rectangle at x=70, y=35, width=60, height=90, rx=30
    mic_x = int(70 * scale)
    mic_y = int(35 * scale)
    mic_width = int(60 * scale)
    mic_height = int(90 * scale)
    mic_radius = int(30 * scale)

    draw.rounded_rectangle(
        [mic_x, mic_y, mic_x + mic_width, mic_y + mic_height],
        radius=mic_radius,
        fill=purple
    )

    # Mic bracket - U-shaped curve
    # Path: M 50 125 Q 50 155 100 155 Q 150 155 150 125
    # This creates a quadratic bezier curve
    bracket_y1 = int(125 * scale)
    bracket_y2 = int(155 * scale)
    bracket_x_left = int(50 * scale)
    bracket_x_center = int(100 * scale)
    bracket_x_right = int(150 * scale)
    bracket_width = max(2, int(12 * scale))

    # Draw U-shape with arc (approximation)
    draw.arc(
        [bracket_x_left, bracket_y1, bracket_x_right, bracket_y2 + int(30 * scale)],
        start=0, end=180,
        fill=purple,
        width=bracket_width
    )

    # Vertical stand - line from (100, 155) to (100, 172)
    stand_x = int(100 * scale)
    stand_y1 = int(155 * scale)
    stand_y2 = int(172 * scale)
    stand_width = max(2, int(12 * scale))

    draw.line(
        [(stand_x, stand_y1), (stand_x, stand_y2)],
        fill=purple,
        width=stand_width
    )

    # Base - horizontal line from (70, 172) to (130, 172)
    base_x1 = int(70 * scale)
    base_x2 = int(130 * scale)
    base_y = int(172 * scale)
    base_width = max(2, int(12 * scale))

    draw.line(
        [(base_x1, base_y), (base_x2, base_y)],
        fill=purple,
        width=base_width
    )

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