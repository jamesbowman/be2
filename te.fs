4   constant columns
64  constant LW

create ff       256 LW * allot 
: line  ( n -- a u )    LW * ff + ;

create linebuf  65 allot

: n. ( n -- )   base @ swap decimal 0 u.r base ! ;
: csi           27 emit '[' emit ;
: normal        csi ." 0m" ;
: fg            csi ." 38;5;" n. ." m" ;
: bg            csi ." 48;5;" n. ." m" ;
: hide          csi ." ?25l" ;
: cursor        csi ." ?25h" ;

form constant w 1- constant h
w columns / constant qw
0 value nl  variable pos 0 pos !
: ml ( -- maxline ) nl 1- ;

: ld  ( filename -- )
    ff 256 LW * blank
    r/o open-file throw >r
    0
    begin
        linebuf 65 r@ read-line throw
    while ( lnum n )
        >r linebuf over line r> move
        1+
    repeat
    drop to nl
    nl . ."  lines read" cr
    r> drop
;

: xy ( row col -- x y )
    over h / qw * +
    swap h mod ;

: lnum  pos @ LW / ;
: cnum  pos @ LW mod ;

: redraw
    utime
    nl 0 do
        i 0 xy at-xy
        i lnum = if 100 else 18 then fg 
        i 1+ 3 .r normal space
        i line LW type
    loop
    0 h at-xy
    utime 2swap d- d. lnum . .s
    lnum cnum 4 + xy at-xy
;

: safe  pos @ 0 max ml LW * min pos ! ;
: go    0 max ml min LW * pos ! ;
: down  1 max LW * pos +! safe ; 
: up    1 max LW * negate pos +! safe ; 

: visual
    page 0
    begin
        hide redraw cursor
        ekey ekey>char if ( c )
            dup '0' '9' 1+ within if
                '0' - swap 10 * +
            else
                case
                13      of down endof
                'G'     of dup 0= nl and + 1- go endof
                'k'     of up endof
                'j'     of down endof
                'q'     of page exit endof
                27      of page exit endof
                        . abort
                endcase 0
            then
        else ekey>fkey if ( key-id )
            case
            k-up    of up endof
            k-down  of down endof
                    drop
            endcase 0
        else ( keyboard-event )
        drop \ just ignore an unknown keyboard event type
        then then
    again
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

s" vibe-2.1ans.fs" ld   visual
