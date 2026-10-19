"""decode.py IN.cells.txt OUT.jsonl  -- rebuilds the writer's JSONL text, one line per cell."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import codec

def main(src, dst):
    with open(src, encoding='utf-8') as f, open(dst, 'w', encoding='utf-8', newline='\n') as g:
        for cell in f:
            cell = cell.rstrip('\n')
            if cell:
                g.write(codec.decode_to_line(cell) + '\n')

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
