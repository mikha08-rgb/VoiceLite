import re

with open('VoiceLite/VoiceLite/Services/PersistentWhisperService.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Find and fix the malformed line
for i in range(len(lines)):
    if 'private static readonly char[] LineSeparators' in lines[i]:
        # Replace lines 24-26 with correct version
        lines[i] = "        private static readonly char[] LineSeparators = new[] { '\r', '\n' };\n"
        # Remove following lines if they're part of the malformed declaration
        if i + 1 < len(lines) and (lines[i+1].strip() == "', '" or lines[i+1].strip().startswith("',")):
            lines[i+1] = ""
        if i + 2 < len(lines) and (lines[i+2].strip() == "' };" or lines[i+2].strip().startswith("' }") or lines[i+2].strip() == "};"):
            lines[i+2] = ""
        break

with open('VoiceLite/VoiceLite/Services/PersistentWhisperService.cs', 'w', encoding='utf-8') as f:
    f.writelines(lines)

print("Fixed LineSeparators array declaration")
