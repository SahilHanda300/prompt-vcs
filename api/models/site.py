from pydantic import BaseModel
from datetime import datetime


class SiteListItem(BaseModel):
    refname: str
    targethash: str
    prompttype: str
    systemtemplate: str
    commitmessage: str
    submittedby: str
    country: str
    inputlabel: str | None
    inputplaceholder: str | None
    outputlabel: str | None
    updatedat: datetime
    goldenscore: float | None
    judgescore: float | None
    promotionreason: str | None


class SiteDashboardItem(BaseModel):
    refname: str
    targethash: str
    prompttype: str
    submittedby: str
    country: str
    commitmessage: str
    createdat: datetime
    updatedat: datetime
    goldenscore: float | None
    judgescore: float | None
    regressionflag: bool | None
    promotionstage: str | None
    promotionreason: str | None
    runat: datetime | None
