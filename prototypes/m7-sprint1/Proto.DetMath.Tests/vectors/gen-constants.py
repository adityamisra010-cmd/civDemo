import struct
from decimal import Decimal as D, getcontext
getcontext().prec = 120
def bits(f): return struct.unpack('<Q', struct.pack('<d', f))[0]
def pi_dec():
    # Machin: pi = 16 atan(1/5) - 4 atan(1/239)
    def atan_inv(n):
        x = D(1)/n; x2 = x*x; s = x; t = x; k = 1
        while True:
            t = -t*x2; k += 2; term = t/k
            if abs(term) < D(10)**-118: break
            s += term
        return s
    return 16*atan_inv(5) - 4*atan_inv(239)
ln2 = D(2).ln()
hi = float(ln2); b = bits(hi) & ~((1<<21)-1)  # keep 32 significant bits (52-21+1)
hi = struct.unpack('<d', struct.pack('<Q', b))[0]
mid = float(ln2 - D(hi)); lo = float(ln2 - D(hi) - D(mid))
print('LN2_HI', hex(bits(hi)), hi); print('LN2_MID', hex(bits(mid)), mid); print('LN2_LO', hex(bits(lo)), lo)
pio2 = pi_dec()/2
rem = pio2; parts=[]
for i in range(4):
    f = float(rem); parts.append(f); rem -= D(f)
for i,f in enumerate(parts): print('PIO2_%d'%(i+1), hex(bits(f)), repr(f))
print('residual', rem)
print('LN2 hi exact 32 bits check', bin(bits(hi) & ((1<<52)-1)))
