export interface SiteListItem {
  refname: string
  targethash: string
  prompttype: string
  systemtemplate: string
  commitmessage: string
  submittedby: string
  country: string
  inputlabel: string | null
  inputplaceholder: string | null
  outputlabel: string | null
  updatedat: string
  goldenscore: number | null
  judgescore: number | null
  promotionreason: string | null
}

export interface SiteDashboardItem {
  refname: string
  targethash: string
  prompttype: string
  submittedby: string
  country: string
  commitmessage: string
  createdat: string
  updatedat: string
  goldenscore: number | null
  judgescore: number | null
  regressionflag: boolean | null
  promotionstage: string | null
  promotionreason: string | null
  runat: string | null
}

export interface QAFailureItem {
  evalid: string
  prompthash: string
  refname: string | null
  goldenscore: number | null
  judgescore: number | null
  stage: string
  promotionreason: string | null
  runat: string
  submittedby: string
  commitmessage: string
}

export interface PromotionAuditItem {
  promptname: string | null
  stage: string
  goldenscore: number | null
  judgescore: number | null
  regressionflag: boolean
  promotionreason: string | null
  runat: string
  submittedby: string
  commitmessage: string
}

export interface PromptHistoryItem {
  contenthash: string
  systemtemplate: string | null
  usertemplate: string | null
  submittedby: string
  country: string
  commitmessage: string
  parenthash: string | null
  prompttype: string
  createdat: string
  dev_current: boolean | null
  qa_current: boolean | null
  prod_current: boolean | null
  goldenscore: number | null
  judgescore: number | null
  regressionflag: boolean | null
  evalstage: string | null
}

export interface DegradedTestCase {
  resultid: string
  testcaseindex: number
  inputtext: string
  expectedoutput: string
  actualoutput: string
  goldenscore: number
  judgescore: number
  judgereasoning: string | null
  createdat: string
}

export interface ChatMessage {
  role: 'user' | 'assistant'
  content: string
  streaming?: boolean
}
