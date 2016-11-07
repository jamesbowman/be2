
: n. ( n -- )   base @ swap decimal 0 u.r base ! ;
: csi           27 emit '[' emit ;
: normal        csi ." 0m" ;
: fg            csi ." 38;5;" n. ." m" ;
: bg            csi ." 48;5;" n. ." m" ;
: hide          csi ." ?25l" ;
: cursor        csi ." ?25h" ;
: el            csi ." K" ;

: .c
    dup fg
    4 u.r
;

: bars
    cr 16 0 do i .c loop

    216 0 do
        i 36 mod 0= if cr then
        i 6 mod 0= if cr then
        i 16 + .c
    loop

    cr 256 232 do i .c loop
;

cr bars cr cr bye
