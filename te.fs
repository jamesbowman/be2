create ff       256 64 * allot 
: line  ( n -- a u )    64 * ff + 64 ;
create linebuf  65 allot

: n. ( n -- )   base @ swap decimal 0 u.r base ! ;
: csi           27 emit '[' emit ;
: normal        csi ." 0m" ;
: fg            csi ." 38;5;" n. ." m" ;
: bg            csi ." 48;5;" n. ." m" ;

form constant w 1- constant h
w 4 / constant qw
0 value ml  0 value lnum

: ld  ( filename -- )
    ff 256 64 * blank
    r/o open-file throw >r
    0
    begin
        linebuf 65 blank
        linebuf 65 r@ read-line throw
    while
        drop ( width not needed )
        linebuf over line move
        1+
    repeat
    drop to ml
    ml . ."  lines read" cr
    r> drop
;

: show
    page
    ml 0 do
        i h / qw * i h mod at-xy
        i lnum = if 100 else 18 then fg 
        i 1+ 3 .r normal space
        i line type
    loop
    0 h 1- at-xy
;

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
s" 8080.fs" ld show
