import { Select, type SelectOption, type SelectProps } from './Select';

export type LanguageOption = SelectOption;
export type LanguageSelectProps = SelectProps;

/** A language picker. Same as Select; kept as its own name for readability at call sites. */
export const LanguageSelect = Select;
