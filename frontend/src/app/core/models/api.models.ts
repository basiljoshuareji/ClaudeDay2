/** Types mirroring the ASP.NET Core API contracts (see backend/src/TodoApp.Api/Dtos). */

export type ThemeName = 'light' | 'dark' | 'ocean' | 'forest' | 'sunset';
export type Priority = 'Low' | 'Medium' | 'High';
export type TaskStatus = 'Pending' | 'InProgress' | 'Completed';
export type DueFilter = 'Today' | 'Upcoming' | 'Overdue' | 'None';
export type SortBy = 'Manual' | 'DueDate' | 'Priority' | 'Created' | 'Title';
export type SortDir = 'Asc' | 'Desc';

export interface User {
  id: number;
  email: string;
  displayName: string;
}

export interface Preferences {
  theme: ThemeName;
  sidebarCollapsed: boolean;
}

export interface Session {
  user: User;
  preferences: Preferences;
}

export interface AuthResponse extends Session {
  accessToken: string;
  /** ISO timestamp (UTC). */
  expiresAt: string;
}

export interface RegisterPayload {
  email: string;
  password: string;
  displayName: string;
  theme?: ThemeName;
}

export interface Section {
  id: number;
  name: string;
  color: string;
  icon: string;
  sortOrder: number;
  isDefault: boolean;
  totalCount: number;
  /** Everything not completed (Pending + InProgress). */
  pendingCount: number;
  inProgressCount: number;
  completedCount: number;
}

export interface SectionPayload {
  name: string;
  color: string;
  icon: string;
}

export interface Tag {
  id: number;
  name: string;
  color: string;
}

export interface TagListItem extends Tag {
  taskCount: number;
}

export interface SubTask {
  id: number;
  taskId: number;
  title: string;
  isCompleted: boolean;
  sortOrder: number;
}

export interface Task {
  id: number;
  sectionId: number;
  title: string;
  description: string | null;
  /** yyyy-MM-dd, no time component. */
  dueDate: string | null;
  priority: Priority;
  status: TaskStatus;
  sortOrder: number;
  completedAt: string | null;
  createdAt: string;
  updatedAt: string;
  tags: Tag[];
  subTasks: SubTask[];
}

export interface SubTaskInput {
  id?: number;
  title: string;
  isCompleted: boolean;
}

export interface TaskPayload {
  sectionId: number;
  title: string;
  description: string | null;
  dueDate: string | null;
  priority: Priority;
  status: TaskStatus;
  tagIds: number[];
  subTasks?: SubTaskInput[];
}

export interface TaskQuery {
  search?: string;
  status?: TaskStatus | '';
  priority?: Priority | '';
  due?: DueFilter | '';
  tagId?: number | null;
  sectionId?: number | null;
  sortBy?: SortBy;
  sortDir?: SortDir | '';
  page?: number;
  pageSize?: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ApiErrorBody {
  status: number;
  code: string;
  message: string;
  errors?: Record<string, string[]> | null;
  traceId?: string | null;
}
