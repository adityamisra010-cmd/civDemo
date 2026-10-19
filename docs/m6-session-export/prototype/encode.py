"""encode.py IN.jsonl OUT.cells.txt  -- one encoded cell per output line."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import codec

def main(src, dst):
    with open(src, encoding='utf-8') as f, open(dst, 'w', encoding='utf-8', newline='\n') as g:
        for n, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            g.write(codec.encode_line(line) + '\n')

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
