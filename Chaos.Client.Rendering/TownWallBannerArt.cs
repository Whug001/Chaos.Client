namespace Chaos.Client.Rendering;

/// <summary>
///     The green Mileth wall banner painted into the Town Hall's wall texture, as the server's wall art has it: the
///     <see cref="WIDTH" /> × <see cref="HEIGHT" /> texels of Unora's <c>Tools/ArenaArt/art/civicMileth/m_wall3.png</c>
///     from (<see cref="U0" />, <see cref="V0" />), RGBA, alpha 0 where the texel is plain wall (as in <c>m_wall0.png</c>).
///     Regenerate both together if that art changes.
/// </summary>
public static class TownWallBannerArt
{
    public const int TEXTURE_SIZE = 112;
    public const int U0 = 41;
    public const int V0 = 3;
    public const int WIDTH = 30;
    public const int HEIGHT = 47;

    public static readonly byte[] Rgba = Convert.FromBase64String(
        ""
        + "NDU4/zQ1OP80NTj/NDU4/zQ1OP80NTj/NDU4/zQ1OP80NTj/NDU4/zQ1OP80NTj/NDU4/zQ1OP80NTj/NDU4/zQ1OP80NTj/NDU4/zQ1OP80NT"
        + "j/NDU4/zQ1OP80NTj/NDU4/zQ1OP80NTj/NDU4/zQ1OP80NTj/Vlhc/1ZYXP9WWFz/Vlhc/1ZYXP9WWFz/Vlhc/1ZYXP9WWFz/Vlhc/1ZYXP9W"
        + "WFz/Vlhc/1ZYXP9WWFz/Q0VH/0NFR/9DRUf/Q0VH/0NFR/9DRUf/Q0VH/0NFR/9DRUf/Q0VH/0NFR/9DRUf/Q0VH/0NFR/9DRUf/sZFcALSTXg"
        + "CegDz/kXU3/y5QKP8tTif/LlAo/zFWK/82Xi//O2cz/z9uN/9Bcjn/QXI5/z9uN/87ZzP/Nl4v/zFWK/8uUCj/LU4n/y5QKP8xViv/Nl4v/ztn"
        + "M/8/bjf/QXI5/0FyOf+4lUb/rYtC/3ZhPf+TeU3/ro5bALSTXgCegDz/kHU3/y5QKP8tTif/LlAo/zFWK/82Xi//OmYz/z5tN/9BcTn/QXE5/z"
        + "5tN/86ZjP/Nl4v/zFWK/8uUCj/LU4n/y5QKP8xViv/Nl4v/zpmM/8+bTf/QXE5/0FxOf+4lUb/rItB/3VgPf+SeEv/ro5aALOSXQCefzz/kHQ3"
        + "/y5QKP8sTif/LlAo/zFWK/81Xi//OmYz/z5tN/9BcTn/QXE5/z5tN/86ZjP/NV4v/zFWK/8uUCj/LE4n/y5QKP8xViv/NV4v/zpmM/8+bTf/QX"
        + "E5/0FxOf+4lEb/rItB/3JeO/+PdUr/sJBcAKuMWQCdfzz/kHQ3/y5QKP8sTif/LlAo/zFWK/81Xi//OmYz/z5tNv9AcTj/QHE4/z5tNv86ZjP/"
        + "NV4v/zFWK/8uUCj/LE4n/y5QKP8xViv/NV4v/zpmM/8+bTb/QHE4/0BxOP+3lEb/q4tB/25bOf+JcUf/mn5QAJZ7TgCdfzz/kHQ3/y1QKP8sTi"
        + "f/LVAo/zFVK/81XS//OmYz/z5tNv9AcTj/QHE4/z5tNv86ZjP/NV0v/zFVK/8tUCj/LE4n/y1QKP8xVSv/NV0v/zpmM/8+bTb/QHE4/0BxOP+3"
        + "lEX/q4pB/2BOMv94Yj7/e2A7AHheOQCdfjv/j3Q2/y1QKP8sTif/LVAo/zFVK/81XS//OmYz/z5sNv9AcDj/QHA4/z5sNv86ZjP/NV0v/zFVK/"
        + "8tUCj/LE4n/y1QKP8xVSv/NV0v/zpmM/8+bDb/QHA4/0BwOP+2k0X/q4pB/1I/J/9jTi//1LBuAM2pagCcfjv/j3M2/y1PKP8sTSf/LU8o/zBV"
        + "Kv81XS7/OmUz/z5sNv9AcDj/QHA4/z5sNv86ZTP/NV0u/zBVKv8tTyj/LE0n/y1PKP8wVSr/NV0u/zplM/8+bDb/QHA4/0BwOP+2k0X/qopB/5"
        + "J4TP+tj1r/waBlALqaYQCcfjv/j3M2/y1PKP8sTSf/LU8o/zBVKv81XS7/OmUz/z5sNv9AcDj/QHA4/z5sNv86ZTP/NV0u/zBVKv8tTyj/LE0n"
        + "/y1PKP8wVSr/NV0u/zplM/8+bDb/QHA4/0BwOP+2k0X/qolB/4VuRv+dg1L/v55jALmZYACcfjv/jnM2/y1PKP8sTSf/LU8o/zBVKv81XC7/OW"
        + "Uy/z1sNv9AcDj/QHA4/z1sNv85ZTL/NVwu/zBVKv8tTyj/LE0n/y1PKP8wVSr/NVwu/zllMv89bDb/QHA4/0BwOP+1kkX/qolA/4ZuRv+fg1P/"
        + "upphALWWXgCbfTv/jnM2/y1PJ/8sTSb/LU8n/zBUKv81XC7/OWUy/6yLQf+ykET/QG84/6yLQf85ZTL/NVwu/4dtM/8tTyf/e2Mv/35mMP8wVC"
        + "r/NVwu/zllMv89bDb/QG84/0BvOP+1kkX/qYlA/4hwR/+dglL/upphALOUXQCbfTv/jnI2/y1PJ/8sTSb/LU8n/zBUKv80XC7/OWQy/6uKQf+y"
        + "kET/P284/6uKQf85ZDL/NFwu/4dtM/8tTyf/emMv/35mMP8wVCr/NFwu/zlkMv89azb/P284/z9vOP+0kkX/qYhA/4hxR/+dglL/tpdfALCRWw"
        + "CbfTv/jXI2/y1OJ/8sTCb/LU4n/zBUKv80XC7/OWQy/6uKQf+xj0P/P283/6uKQf85ZDL/NFwu/4ZsM/8tTif/emMu/31lMP8wVCr/NFwu/zlk"
        + "Mv89azb/P283/z9vN/+0kUT/qIhA/4dwRv+dglL/s5VdAK+RWwCafTv/jXI2/y1OJ/8sTCb/LU4n/zBUKv80XC7/OWQy/6uKQf+xj0P/sY9D/6"
        + "uKQf+ggT3/knY4/4ZsM/99ZTD/emMu/31lMP8wVCr/NFwu/zlkMv89azX/P283/z9vN/+0kUT/qIhA/4RtRf+cgFH/rI9aALGSXACafDr/jXI1"
        + "/y1OJ/8rTCb/LU4n/zBUKv80Wy7/OWQy/6qKQf+wj0P/sI9D/6qKQf+fgT3/knY4/4ZsM/99ZS//emIu/31lL/8wVCr/NFsu/zlkMv89azX/P2"
        + "43/z9uN/+zkUT/qIdA/4ZvRv+dgVH/qoxYALCSWwCZfDr/jHE1/yxOJ/8rTCb/LE4n/zBTKv80Wy7/OWQy/6qJQf+wjkP/sI5D/6qJQf+fgDz/"
        + "knY3/4VsM/99ZS//eWIu/31lL/8wUyr/NFsu/zlkMv89ajX/P243/z9uN/+zkET/p4dA/4NsRP+Zf1D/mH5PAJR6TQCZfDr/jHE1/yxOJ/8rTC"
        + "b/LE4n/y9TKv80Wy7/OWMy/6qJQP+wjkP/sI5D/6qJQP+fgDz/kXY3/4VsM/98ZC//eWIu/3xkL/8vUyr/NFsu/zljMv88ajX/P243/z9uN/+y"
        + "kET/p4c//2xZOf+AaUP/fWI8AHleOgCZezr/jHE1/yxOJ/8rTCb/LE4n/y9TKv80Wy3/OGMy/6mJQP+vjkP/r45D/6mJQP+egDz/kXU3/4VrMv"
        + "98ZC//eWIu/3xkL/8vUyr/NFst/zhjMv88ajX/P243/z9uN/+ykET/p4c//1A/Jv9kTi//x6BnAMGcZACYezr/i3E1/yxNJ/8rSyb/LE0n/y9T"
        + "Kf80Wy3/OGMx/6mIQP+vjUL/r41C/6mIQP+egDz/kXU3/4RrMv98ZC//eWEu/3xkL/8vUyn/NFst/zhjMf88ajX/Pm03/z5tN/+ykET/poY//3"
        + "1lQf93Xjr/sY9cAK6MWgCYezr/i3A1/yxNJ/8rSyb/LE0n/y9TKf80Wi3/OGMx/6iIQP+ujUL/ro1C/6iIQP+efzz/kHU3/4RrMv97ZC//eGEu"
        + "/3tkL/8vUyn/NFot/zhjMf88aTX/Pm03/z5tN/+xj0P/poY//29ZOf93Xjr/spBcAK+NWwCYezr/i3A1/yxNJ/8rSyb/LE0n/y9TKf8zWi3/OG"
        + "Ix/6iIQP+ujUL/ro1C/6iIQP+dfzz/kHQ3/4RrMv97Yy//eGEu/3tjL/8vUyn/M1ot/zhiMf88aTX/Pm02/z5tNv+xj0P/pYY//2xXOP92Xjn/"
        + "sY9cAKyLWgCXejr/i3A1/yxNJv8rSyX/LE0m/y9SKf8zWi3/OGIx/6iHQP+ujEL/roxC/6iHQP84YjH/M1ot/4RqMv97Yy//eGEu/3tjL/8vUi"
        + "n/M1ot/zhiMf88aTT/Pm02/z5tNv+wj0P/pYU//2tWN/92XTn/rYxaAKmIWACXejn/inA1/yxNJv8rSyX/LE0m/y9SKf8zWi3/OGIx/6eHQP+t"
        + "jEL/rYxC/6eHQP84YjH/M1ot/4NqMv97Yy//d2Et/3tjL/8vUin/M1ot/zhiMf88aTT/Pmw2/z5sNv+wjkP/pYU//2lVN/90XDn/q4pZAKiHVw"
        + "CXejn/im80/yxNJv8rSyX/LE0m/y9SKf8zWi3/OGIx/6eHP/+tjEL/rYxC/6eHP/84YjH/M1ot/4NqMv96Yy7/d2At/3pjLv8vUin/M1ot/zhi"
        + "Mf88aDT/Pmw2/z5sNv+wjkP/pIU+/2hTNf91XTn/pYVWAKqJWACWeTn/im80/yxMJv8qSiX/LEwm/y9SKf8zWS3/OGEx/6aGP/+ti0L/rYtC/6"
        + "aGP/84YTH/M1kt/4NqMv96Yy7/d2At/3pjLv8vUin/M1kt/zhhMf87aDT/Pmw2/z5sNv+vjkP/pIQ+/2pWN/94Xjr/ooNUAKmIWACWeTn/iW80"
        + "/ytMJv8qSiX/K0wm/y9SKf8zWS3/N2Ex/6aGP/+si0H/rItB/6aGP/83YTH/M1kt/4JpMv96Yi7/d2At/3piLv8vUin/M1kt/zdhMf87aDT/PW"
        + "w2/z1sNv+vjUL/pIQ+/2lVNv94Xzr/oIFTAKeHVwCWeTn/iW80/ytMJv8qSiX/K0wm/y5RKf8zWSz/N2Ex/6aGP/+si0H/rItB/6aGP/83YTH/"
        + "M1ks/4JpMf95Yi7/dmAt/3liLv8uUSn/M1ks/zdhMf87aDT/PWs2/z1rNv+ujUL/o4Q+/2hUNv94Xzr/jHFJAJB1SwCVeTn/iW40/ytMJv8qSi"
        + "X/K0wm/y5RKf8zWSz/N2Ew/ztnNP89azb/PWs2/ztnNP83YTD/M1ks/y5RKf8rTCb/Kkol/ytMJv8uUSn/M1ks/zdhMP87ZzT/PWs2/z1rNv+u"
        + "jUL/o4Q+/1tJL/94Xjr/d105AHpfOgCVeDn/iG40/ytMJv8qSiX/K0wm/y5RKf8yWSz/N2Ew/ztnNP89azX/PWs1/ztnNP83YTD/Mlks/y5RKf"
        + "8rTCb/Kkol/ytMJv8uUSn/Mlks/zdhMP87ZzT/PWs1/z1rNf+ujEL/ooM+/1RBKP9nUDH/so9cALmVYACVeDj/iG40/ytMJv8qSiX/K0wm/y5R"
        + "KP8yWCz/N2Aw/ztnM/89azX/PWs1/ztnM/83YDD/Mlgs/y5RKP8rTCb/Kkol/ytMJv8uUSj/Mlgs/zdgMP87ZzP/PWs1/z1rNf+tjEL/ooM+/3"
        + "VePf94Xzv/nn9SAKWEVQCUeDj/iG40/ytLJv8qSSX/K0sm/y5RKP8yWCz/N2Aw/ztnM/89ajX/PWo1/ztnM/83YDD/Mlgs/y5RKP8rSyb/Kkkl"
        + "/ytLJv8uUSj/Mlgs/zdgMP87ZzP/PWo1/z1qNf+tjEL/ooM9/2tWN/96YTz/nn9SAKODVACUeDj/h20z/ytLJv8qSSX/K0sm/y5QKP8yWCz/N2"
        + "Aw/zpnM/89ajX/PWo1/zpnM/83YDD/Mlgs/y5QKP8rSyb/Kkkl/ytLJv8uUCj/Mlgs/zdgMP86ZzP/PWo1/z1qNf+si0L/oYI9/2pVN/96YTv/"
        + "nH1RAKCAUwCUdzj/h20z/ytLJf8qSSX/K0sl/y5QKP8yWCz/N2Aw/zpmM/88ajX/PGo1/zpmM/83YDD/Mlgs/y5QKP8rSyX/Kkkl/ytLJf8uUC"
        + "j/Mlgs/zdgMP86ZjP/PGo1/zxqNf+si0H/oYI9/2hUNv95YDv/mnxQAJ+AUgCTdzj/h20z/ytLJf8qSST/K0sl/y5QKP8yWCz/Nl8w/zpmM/88"
        + "ajX/PGo1/zpmM/82XzD/Mlgs/y5QKP8rSyX/Kkkk/ytLJf8uUCj/Mlgs/zZfMP86ZjP/PGo1/zxqNf+si0H/oYI9/2dSNf95YDv/nn9SAJh6Tw"
        + "CTdzj/hm0z/ytLJf8pSST/K0sl/y5QKP8yVyz/Nl8w/zpmM/88ajX/PGo1/zpmM/82XzD/Mlcs/y5QKP8rSyX/KUkk/ytLJf8uUCj/Mlcs/zZf"
        + "MP86ZjP/PGo1/zxqNf+rikH/oIE9/2NQM/92Xjn/nX5RAJh5TgCTdjj/hmwz/ypKJf8pSST/Kkol/y1QKP8yVyz/Nl8w/zpmM/88aTX/PGk1/z"
        + "pmM/82XzD/Mlcs/y1QKP8qSiX/KUkk/ypKJf8tUCj/Mlcs/zZfMP86ZjP/PGk1/zxpNf+rikH/oIE9/2FOM/93Xjn/lnhOAJp8UACSdjj/hmwz"
        + "/ypKJf8pSCT/Kkol/y1QKP8yVyv/Nl8v/zplM/88aTX/PGk1/zplM/82Xy//Mlcr/y1QKP8qSiX/KUgk/ypKJf8tUCj/Mlcr/zZfL/86ZTP/PG"
        + "k1/zxpNf+qikH/n4E9/2NQM/95YDv/lXhNAJh6TwCSdjf/hWwz/ypKJf8pSCT/Kkol/y1PKP8xVyv/Nl8v/zplM/88aTT/PGk0/zplM/82Xy//"
        + "MVcr/y1PKP8qSiX/KUgk/ypKJf8tTyj/MVcr/zZfL/86ZTP/PGk0/zxpNP+qiUH/n4A8/2NPM/95YDv/g2lEAIZrRQCSdjf/hWwz/ypKJf8pSC"
        + "T/Kkol/y1PKP8xVyv/Nl4v/zllMv88aTT/PGk0/zllMv82Xi//MVcr/y1PKP8qSiX/KUgk/ypKJf8tTyj/MVcr/zZeL/85ZTL/PGk0/zxpNP+q"
        + "iUD/n4A8/1dGLP96YDv/eF46AHxhOwCRdTf/hWsy/ypKJf8pSCT/Kkol/y1PJ/8xViv/Nl4v/zllMv87aDT/O2g0/6mJQP+egDz/kXU3/4VrMv"
        + "8qSiX/KUgk/ypKJf8tTyf/MVYr/zZeL/85ZTL/O2g0/ztoNP+piUD/noA8/1JAJ/9pUjL/upVhAL+YYwCRdTf/hWsy/ypKJf8pSCT/Kkol/y1P"
        + "J/8xViv/Nl4v/zlkMv+vjUL/r41C/6mIQP+egDz/kXU3/4VrMv98ZC//eWEu/ypKJf8tTyf/MVYr/zZeL/85ZDL/O2g0/ztoNP+piED/noA8/3"
        + "hgPf+VeE3/qYdXAI1wSQCRdTf/hGsy/ypJJf8pSCT/Kkkl/y1PJ/8xViv/nn88/6iIQP+vjUL/r41C/6OCVAChgFMAm3xRAJl6TwB7ZC//eGEu"
        + "/3tkL/+EazL/MVYr/zVeL/85ZDL/O2g0/ztoNP+oiED/nn88/2xXOP+Ga0X/poVWAKmHWACQdDf/hGsy/ypJJf8pRyT/e2Mv/4RrMv+QdDf/nX"
        + "88/6iIQP+ff1MAoYFUAIRqRQCggFMAm3tQAJd5TwCNcEUApIRVAHtjL/+EazL/kHQ3/51/PP+oiED/O2g0/ztoNP+oiED/nX88/2pWN/+Ga0X/"
        + "pINVAKiGVwCQdDf/hGoy/3tjL/94YS7/e2Mv/4RqMv+QdDf/mntQAJh5TwCcfVEAoIBTAKCAUwCdfVEAmXpPAJV3TQCOcEUAoYJUAJx9UQChgV"
        + "MAkHQ3/51/PP+oh0D/roxC/66MQv+oh0D/nX88/5x9UQCef1IAqIZXAKGBVACPdDf/g2oy/3tjL/93YC3/mXtQAJx8UQCZek8AnX1RAJt7UACX"
        + "eE4AmHpPAJh6TwCXeU4Am3xQAJh5TwCRc0cApYVWAJZ5TgCZe1AAoIFTAJ5/UgCff1IArYxC/62MQv+nh0D/nH47/5Z4TgCYek8ApYRVAJ9/Uw"
        + "CPdDb/g2oy/5l6TwCbfFEAmXpPAJp7UACYeU8Am3xQAJl6UACUdk0AmHlPAJh5TwCVd00AmntQAJZ4TgCRc0cAooJUAJR3TQCYe08AnX9SAJx+"
        + "UQCdflEAnn9SAJl7TwCnhz//nH47/5N2TACWeU4A");
}
