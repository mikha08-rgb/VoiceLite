with open('VoiceLite/VoiceLite/Services/PersistentWhisperService.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Just remove the static field entirely - line 446 already has the correct inline version
import re
content = re.sub(
    r'\s*private static readonly char\[\] LineSeparators = .*?;[\r\n]*',
    '\n',
    content,
    flags=re.DOTALL
)

with open('VoiceLite/VoiceLite/Services/PersistentWhisperService.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print("Removed malformed LineSeparators static field")
