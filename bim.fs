
: blank buffer update dup 1024 [char] . fill ;    \ new blank block
: bu    block update ;                      \ obtain block, mark it modified
: prev@                 block @ ;
: next@                 block cell+ @ ;
: prev! ( x y -- )      bu ! ;
: next! ( x y -- )      bu cell+ ! ;

: append  ( b a -- caddr ) \ append b after a
    dup next@ >r            \ save c
    2dup next!              \ set a's next to b

    over r@ prev!           \ set c's prev to b

    swap blank              ( a b-buf )
    r> over cell+ !         \ set b's next to c
    tuck !                  \ set b's prev to a
    cell+ cell+
;

: delete  ( b -- ) \ delete b
    dup prev@ swap next@    ( a c )
    2dup prev!              \ set c's prev to a
    swap next!              \ set a's next to b
;

variable FR

: cold
    1 blank 2 cells erase
    1 FR !
    10 2 do
        i s>d <# #s [char] # hold #>
        i 1 append
        swap move
    loop
;

: head ( a -- a b ) \ delete a from list, return parts
    dup next@
    over delete
;

: get ( -- a )  \ get a block from FR list
    FR @ head FR !
;

: 1more ( b -- c caddr) \ new line after b
    ?dup if
        get                     ( b c )
        tuck swap append        ( c caddr )
    else
        get dup block
        dup 2 cells erase
        2 cells +
    then
;

: b? ( u -- )
    cr
    dup 4 u.r [char] : emit space
    block
    dup @ 4 u.r space
    dup cell+ @ 4 u.r 4 spaces
    cell+ cell+ 100 type
;

: x
    10 1 do
        i b?
    loop
;

: lst
    begin
        ?dup
    while
        dup b?
        next@
    repeat
;

: <<  ( b -- b ) \ rewind to start
    begin
        dup prev@   ( b a )
        ?dup
    while
        nip
    repeat
;

: e ( -- )
    bl parse r/o open-file throw >r
    0
    begin
        pad 1000 r@ read-line throw
    while
                        ( b len )
        >r
        1more           ( b caddr )
        pad swap r>     ( b pad caddr len )
        cmove

    repeat
    drop
    r> close-file throw
    <<
;

( Terminal handling )
: n. ( n -- ) base @ swap decimal 0 u.r base ! ;
: csi       27 emit [char] [ emit ;

: normal        csi ." 0m" ;
: fg            csi ." 38;5;" n. ." m" ;
: bg            csi ." 48;5;" n. ." m" ;

: clear-to-end  csi ." 0K" ;

: hide          csi ." ?25l" ;
: show          csi ." ?25h" ;

( Background colors )
: body-color   255 fg 0 bg ;
: title-color   255 fg 235 bg ;
: margin-color   255 fg 238 bg ;
: prompt-color   0 fg 7 bg ;

: screen-columns ( -- n ) form nip ;
: screen-lines ( -- n ) form drop ;

: plot ( b -- )
    screen-lines 0 do
        0 i at-xy
        dup if
            prompt-color
            dup block cell+ cell+ screen-columns type
            next@
        else
            normal 16 5 + fg
            [char] ~ emit
            clear-to-end
        then
    loop
    drop
;

cold
e short

: show
    dup fg
    4 u.r
;

: bars
    cr 16 0 do i show loop

    216 0 do
        i 36 mod 0= if cr then
        i 6 mod 0= if cr then
        i 16 + show
    loop

    cr 256 232 do i show loop
;
